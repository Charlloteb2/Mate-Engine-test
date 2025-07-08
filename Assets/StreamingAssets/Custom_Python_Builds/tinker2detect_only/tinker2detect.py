import tkinter as tk
from tkinter import messagebox, ttk
import soundcard as sc
import numpy as np
import json
import time
import warnings
import threading
import os
import sys
from soundcard import SoundcardRuntimeWarning

warnings.filterwarnings("ignore", category=SoundcardRuntimeWarning)

SAMPLE_RATE = 44100
DURATION = 1
def get_base_path():
    if getattr(sys, 'frozen', False):
        return os.path.dirname(sys.executable)
    else:
        return os.path.dirname(os.path.abspath(__file__))

SCRIPT_DIR = get_base_path()

CONFIG_PATH = os.path.join(SCRIPT_DIR, "bpm_device_config.json")
MIN_VOLUME_THRESHOLD = 0.01

def rms(signal):
    return np.sqrt(np.mean(signal ** 2))

class AudioDetectorApp:
    def __init__(self, root):
        self.root = root
        self.root.title("Detectando saída de áudio")
        self.root.geometry("350x120")
        self.root.resizable(False, False)

        self.label = tk.Label(root, text="Detectando saída de áudio compatível...", font=("Arial", 12))
        self.label.pack(pady=10)

        self.progress = ttk.Progressbar(root, mode='determinate', length=300)
        self.progress.pack(pady=10)

        self.volumes = []
        self.root.after(100, self.start_detection)

    def start_detection(self):
        threading.Thread(target=self.detect_devices, daemon=True).start()

    def detect_devices(self):
        try:
            loopbacks = sc.all_microphones(include_loopback=True)
            if not loopbacks:
                self.root.after(0, lambda: [messagebox.showerror("Erro", "Nenhum dispositivo de saída com loopback encontrado"),
                                            self.root.destroy(), sys.exit()])
                return

            volumes = []
            total = len(loopbacks)
            for i, mic in enumerate(loopbacks):
                with mic.recorder(samplerate=SAMPLE_RATE) as rec:
                    audio = rec.record(numframes=SAMPLE_RATE * DURATION)
                    if len(audio.shape) > 1 and audio.shape[1] > 1:
                        audio = np.mean(audio, axis=1)
                    audio = audio.flatten()
                    level = rms(audio)
                    volumes.append((level, mic.name))
                    time.sleep(0.5)

                self.progress['value'] = ((i + 1) / total) * 100
                self.root.update_idletasks()

            if not volumes:
                self.root.after(0, lambda: [messagebox.showerror("Erro", "Nenhum dispositivo detectado."),
                                            self.root.destroy(), sys.exit()])
                return

            volumes.sort(key=lambda x: -x[0])
            self.volumes = volumes
            self.root.after(0, self.show_selection_window)

        except Exception as e:
            self.root.after(0, lambda: [messagebox.showerror("Erro interno", str(e)),
                                        self.root.destroy(), sys.exit()])

    def show_selection_window(self):
        self.root.withdraw()

        selection_win = tk.Toplevel()
        selection_win.title("Selecionar dispositivo")
        selection_win.geometry("500x360")
        selection_win.resizable(False, False)

        tk.Label(selection_win, text="Selecione o dispositivo de saída para usar:", font=("Arial", 11)).pack(pady=10)

        listbox = tk.Listbox(selection_win, width=65, height=12, font=("Courier New", 10))
        listbox.pack(pady=5)

        for i, (level, name) in enumerate(self.volumes):
            item = f"{i}. {name} — {level:.5f}"
            listbox.insert(tk.END, item)
            if level < MIN_VOLUME_THRESHOLD:
                listbox.itemconfig(i, fg='gray')

        listbox.selection_set(0)

        def on_confirm():
            selection = listbox.curselection()
            if not selection:
                messagebox.showerror("Erro", "Selecione um dispositivo antes de confirmar.")
                return

            idx = selection[0]
            escolhido = self.volumes[idx][1]

            try:
                with open(CONFIG_PATH, "w") as f:
                    json.dump({"preferred_output_device": escolhido}, f, indent=2)
                messagebox.showinfo("Sucesso", f"Dispositivo salvo:\n{escolhido}")
            except Exception as e:
                messagebox.showerror("Erro ao salvar", f"Erro ao salvar o arquivo JSON:\n{e}")

            selection_win.destroy()
            self.root.destroy()
            sys.exit()

        tk.Button(selection_win, text="OK", width=10, command=on_confirm).pack(pady=10)

class SetupIntro:
    def __init__(self):
        self.win = tk.Tk()
        self.win.title("Audio Detect Setup")
        self.win.geometry("400x180")
        self.win.resizable(False, False)

        label = tk.Label(self.win, text="Para prosseguir, abra qualquer player e inicie\nqualquer música, então clique em OK.",
                         font=("Arial", 11), justify="center")
        label.pack(pady=30)

        ok_button = tk.Button(self.win, text="OK", font=("Arial", 10), width=10, command=self.start_detection)
        ok_button.pack()

        self.win.mainloop()

    def start_detection(self):
        self.win.destroy()
        root = tk.Tk()
        app = AudioDetectorApp(root)
        root.mainloop()

if __name__ == "__main__":
    SetupIntro()
