import soundcard as sc
import numpy as np
import time
import json
import warnings
import os
import sys
from soundcard import SoundcardRuntimeWarning
from scipy.signal import butter, lfilter
import pywintypes
import socket


warnings.filterwarnings("ignore", category=SoundcardRuntimeWarning)

# === DIRETÓRIO BASE DO SCRIPT ===

bpmsettings = "bpm_settings.json"
jsonpath = "bpm_config.json"
deviceconfigpath = "bpm_device_config.json"

def fix_dir(dir):
    config_name = dir

    if getattr(sys, 'frozen', False):
        application_path = os.path.dirname(sys.executable)
        running_mode = 'Frozen/executable'
    else:
        try:
            app_full_path = os.path.realpath(__file__)
            application_path = os.path.dirname(app_full_path)
            running_mode = "Non-interactive (e.g. 'python myapp.py')"
        except NameError:
            application_path = os.getcwd()
            running_mode = 'Interactive'

    return os.path.join(application_path, config_name)


BPM_SETTINGS_PATH = fix_dir(bpmsettings)
BPM_JSON_PATH = fix_dir(jsonpath)
DEVICE_CONFIG_PATH = fix_dir(deviceconfigpath)

print(f"{BPM_SETTINGS_PATH}")


# === CARREGAR CONFIGURAÇÕES ===
with open(BPM_SETTINGS_PATH, "r") as f:
    settings = json.load(f)

SAMPLE_RATE = settings.get("SAMPLE_RATE", 44100)
DURATION = settings.get("DURATION", 3)
PRECISION_IN_MS = settings.get("PRECISION_IN_MS", 20)
WINDOW_SIZE = int(PRECISION_IN_MS / 1000 * SAMPLE_RATE)
MIN_BPM = settings.get("MIN_BPM", 60)
MAX_BPM = settings.get("MAX_BPM", 250)
CONSIDERATION_TIME = settings.get("CONSIDERATION_TIME", 8)
LOWCUT = settings.get("LOWCUT", 30.0)
HIGHCUT = settings.get("HIGHCUT", 190.0)
SALVAR_SOMENTE_SE_ESTAVEL = settings.get("salvar_somente_se_estavel", False)
INTERVALO_CONSIDERADO_ESTAVEL = settings.get("intervalo_considerado_estavel", 3)

# === VARIÁVEIS ===
beat_timestamps = []
smoothed_bpm = None
bpm_history = []

# === constantes ===
UDP_IP = "127.0.0.1"
UDP_PORT = 9955

# === FUNÇÕES ===

def send_bpm_via_udp(bpm):
    message = f"{int(bpm)}\n".encode('utf-8')
    try:
        sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        sock.sendto(message, (UDP_IP, UDP_PORT))
        print(f" BPM enviado via UDP: {bpm}")
    except Exception as e:
        print(f" Erro ao enviar BPM via UDP: {e}")


def butter_bandpass(lowcut, highcut, fs, order=2):
    nyq = 0.5 * fs
    low = lowcut / nyq
    high = highcut / nyq
    return butter(order, [low, high], btype='band')

def apply_bandpass_filter(data, lowcut=LOWCUT, highcut=HIGHCUT, fs=SAMPLE_RATE, order=2):
    b, a = butter_bandpass(lowcut, highcut, fs, order=order)
    return lfilter(b, a, data)

def rms(signal):
    signal = signal.astype(np.float64)
    signal = np.nan_to_num(signal, nan=0.0, posinf=0.0, neginf=0.0)
    max_val = np.max(np.abs(signal))
    if max_val > 10.0:
        signal = signal / max_val
    return np.sqrt(np.mean(signal ** 2))

def detect_peaks(energy_list, threshold_ratio=1.05):
    peaks = []
    for i in range(1, len(energy_list) - 1):
        if energy_list[i] > energy_list[i - 1] and energy_list[i] > energy_list[i + 1]:
            if energy_list[i] > np.mean(energy_list) * threshold_ratio:
                peaks.append(i)
    return peaks

def calculate_bpm_from_peaks(peaks_indices, window_duration):
    global beat_timestamps
    timestamps = [i * window_duration for i in peaks_indices]
    now = time.perf_counter()
    beat_timestamps = [now - (timestamps[-1] - t) for t in timestamps if now - (timestamps[-1] - t) > now - CONSIDERATION_TIME]
    if len(beat_timestamps) < 2:
        return None
    intervals = np.diff(beat_timestamps)
    intervals = intervals[(intervals > 60 / MAX_BPM) & (intervals < 60 / MIN_BPM)]
    if len(intervals) == 0:
        return None
    avg_interval = np.mean(intervals)
    bpm = 60 / avg_interval
    if bpm > 0 and bpm < 0.6 * MAX_BPM:
        bpm_doubled = bpm * 2
        if bpm_doubled <= MAX_BPM:
            bpm = bpm_doubled
    return int(round(bpm))

def smooth_bpm(bpm):
    global smoothed_bpm
    if smoothed_bpm is None:
        smoothed_bpm = bpm
    else:
        diff = abs(bpm - smoothed_bpm)
        if diff > 35:
            smoothed_bpm = bpm
        else:
            alpha = 0.7 if diff > 10 else 0.4
            smoothed_bpm = (1 - alpha) * smoothed_bpm + alpha * bpm
    return int(round(smoothed_bpm))

def save_bpm_to_json(bpm):
    try:
        data = {"currentBPM": bpm}
        with open(BPM_JSON_PATH, "w") as f:
            json.dump(data, f)
    except PermissionError:
        print("Permissão negada ao tentar salvar BPM. (Talvez o Unity esteja lendo o arquivo?)")
    except Exception as e:
        print(f"Erro ao salvar BPM: {e}")

def bpm_estavel():
    if len(bpm_history) < 2:
        return not SALVAR_SOMENTE_SE_ESTAVEL
    return abs(bpm_history[-1] - bpm_history[-2]) <= INTERVALO_CONSIDERADO_ESTAVEL

# === MAIN ===

def main():
    loopbacks = sc.all_microphones(include_loopback=True)
    if not loopbacks:
        print("Nenhum dispositivo loopback encontrado.")
        return

    try:
        with open(DEVICE_CONFIG_PATH, "r") as f:
            preferred_name = json.load(f)["preferred_output_device"]
            mic = next((d for d in loopbacks if d.name == preferred_name), None)
    except:
        mic = None

    if mic is None:
        mic = loopbacks[0]

    print(f"Capturando saída de áudio: {mic.name}")

    with mic.recorder(samplerate=SAMPLE_RATE) as recorder:
        try:
            while True:
                print(f"\n[{time.strftime('%H:%M:%S')}] Gravando {DURATION}s...")
                audio = recorder.record(numframes=SAMPLE_RATE * DURATION)

                if len(audio.shape) > 1 and audio.shape[1] > 1:
                    audio = np.mean(audio, axis=1)

                audio = audio.flatten().astype(np.float64)
                audio = np.nan_to_num(audio, nan=0.0, posinf=0.0, neginf=0.0)
                max_val = np.max(np.abs(audio))
                if max_val > 0:
                    audio = audio / max_val

                filtered_audio = apply_bandpass_filter(audio)
                filtered_audio *= 1.5

                energy_windows = []
                for start in range(0, len(filtered_audio), WINDOW_SIZE):
                    window = filtered_audio[start:start + WINDOW_SIZE]
                    if len(window) == 0:
                        continue
                    energy_windows.append(rms(window))

                peaks = detect_peaks(energy_windows)

                if peaks:
                    bpm = calculate_bpm_from_peaks(peaks, WINDOW_SIZE / SAMPLE_RATE)
                    if bpm:
                        bpm = smooth_bpm(bpm)
                        bpm_history.append(bpm)
                        print(f" Batidas detectadas: {len(peaks)} → BPM: {bpm}")
                        if not SALVAR_SOMENTE_SE_ESTAVEL or bpm_estavel():
                            print(" BPM salvo.")
                            send_bpm_via_udp(bpm)
                        else:
                            print(" BPM instável — não salvo.")
                    else:
                        print(f" Batidas detectadas: {len(peaks)}, mas BPM ainda não está estável")
                else:
                    print(" Nenhuma batida detectada")

        except KeyboardInterrupt:
            print("\n Finalizado pelo usuário")

if __name__ == "__main__":
    main()
