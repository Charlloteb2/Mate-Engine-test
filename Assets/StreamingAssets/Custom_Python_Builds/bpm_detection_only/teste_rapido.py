# Exemplo mínimo para enviar uma linha via named pipe em Windows

import pywintypes
import win32file
import win32pipe
import time

PIPE_NAME = r'\\.\pipe\bpm_pipe'

def send_bpm_via_pipe(bpm):
    try:
        pipe = win32file.CreateFile(
            PIPE_NAME,
            win32file.GENERIC_WRITE,
            0, None,
            win32file.OPEN_EXISTING,
            0, None)

        data = f"{bpm}\n".encode('utf-8')  # IMPORTANTE: enviar \n para ReadLine do Unity

        win32file.WriteFile(pipe, data)
        win32file.CloseHandle(pipe)
        print(f"BPM enviado via pipe: {bpm}")
    except pywintypes.error as e:
        print(f"Erro ao enviar BPM via pipe: {e}")

# Teste rápido
if __name__ == "__main__":
    while True:
        bpm = 120  # exemplo fixo ou calcule seu bpm aqui
        send_bpm_via_pipe(bpm)
        time.sleep(1)
