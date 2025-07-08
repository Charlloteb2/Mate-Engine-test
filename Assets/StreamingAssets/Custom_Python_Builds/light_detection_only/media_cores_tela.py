import time
import os
import json
import numpy as np
from PIL import ImageGrab
import colorsys

CONFIG_PATH = os.path.join(os.path.dirname(sys.executable), "light_settings.json")


# === CONFIGURAÇÕES ===
FPS = 30.0
CAMINHO_JSON = os.path.join(os.path.dirname(sys.executable), "color_data.json")
AUMENTO_SATURACAO = 10.0
TIME_PER_FRAME = 1.0 / FPS

# Controle de intensidade baseada em saturação
EXPONENTE_SATURACAO = 1.3  # quanto maior, mais peso a saturação tem
GANHO_INTENSIDADE = 25.0    # fator final multiplicador da intensidade
INTENSIDADE_MIN = 2.0
INTENSIDADE_MAX = 12.0

# ATIVAR / DESATIVAR ATUALIZAÇÃO DAS LUZES AMBIENTAIS
ATUALIZAR_AMBI1 = True
ATUALIZAR_AMBI2 = True
ATUALIZAR_AMBI3 = False
ATUALIZAR_AVATAR = True

# carrega configurações do json
def carregar_configuracoes():
    global FPS, TIME_PER_FRAME, AUMENTO_SATURACAO, EXPONENTE_SATURACAO
    global GANHO_INTENSIDADE, INTENSIDADE_MIN, INTENSIDADE_MAX
    global ATUALIZAR_AMBI1, ATUALIZAR_AMBI2, ATUALIZAR_AMBI3, ATUALIZAR_AVATAR

    if not os.path.exists(CONFIG_PATH):
        print(f"[Aviso] Arquivo de configuração '{CONFIG_PATH}' não encontrado. Usando padrões.")
        return

    try:
        with open(CONFIG_PATH, "r") as f:
            config = json.load(f)

        FPS = float(config.get("FPS", FPS))
        TIME_PER_FRAME = 1.0 / FPS
        AUMENTO_SATURACAO = float(config.get("AUMENTO_SATURACAO", AUMENTO_SATURACAO))
        EXPONENTE_SATURACAO = float(config.get("EXPONENTE_SATURACAO", EXPONENTE_SATURACAO))
        GANHO_INTENSIDADE = float(config.get("GANHO_INTENSIDADE", GANHO_INTENSIDADE))
        INTENSIDADE_MIN = float(config.get("INTENSIDADE_MIN", INTENSIDADE_MIN))
        INTENSIDADE_MAX = float(config.get("INTENSIDADE_MAX", INTENSIDADE_MAX))

        ATUALIZAR_AMBI1 = bool(config.get("ATUALIZAR_AMBI1", ATUALIZAR_AMBI1))
        ATUALIZAR_AMBI2 = bool(config.get("ATUALIZAR_AMBI2", ATUALIZAR_AMBI2))
        ATUALIZAR_AMBI3 = bool(config.get("ATUALIZAR_AMBI3", ATUALIZAR_AMBI3))
        ATUALIZAR_AVATAR = bool(config.get("ATUALIZAR_AVATAR", ATUALIZAR_AVATAR))

        print("[Info] Configurações carregadas do config.json.")
    except Exception as e:
        print(f"[Erro] Falha ao carregar configurações: {e}")



def capturar_media_cor():
    screenshot = ImageGrab.grab()
    screenshot = screenshot.resize((144, 128))
    img_np = np.array(screenshot)

    if img_np.shape[2] == 4:
        img_np = img_np[:, :, :3]

    media_rgb = img_np.mean(axis=(0, 1)) / 255.0
    return tuple(media_rgb)

def aumentar_saturacao(rgb, fator=AUMENTO_SATURACAO):
    h, s, v = colorsys.rgb_to_hsv(*rgb)
    s = min(s * fator, 1.0)
    return colorsys.hsv_to_rgb(h, s, v)

def calcular_intensidade(rgb):
    h, s, v = colorsys.rgb_to_hsv(*rgb)
    intensidade = (s ** EXPONENTE_SATURACAO) * v * GANHO_INTENSIDADE
    return float(np.clip(intensidade, INTENSIDADE_MIN, INTENSIDADE_MAX))

def cor_mudou_significativamente(nova, antiga, tolerancia=0.01):
    return any(abs(a - b) > tolerancia for a, b in zip(nova, antiga))

def atualizar_json(rgb, caminho=CAMINHO_JSON):
    if os.path.exists(caminho):
        with open(caminho, "r") as f:
            dados = json.load(f)
    else:
        dados = {
            "avatar": {"r": 1.0, "g": 1.0, "b": 1.0, "intensity": 0.2},
            "ambi1": {"r": 1.4, "g": 0.8, "b": 1.0, "intensity": 0.0},
            "ambi2": {"r": 1.0, "g": 0.5, "b": 0.5, "intensity": 0.0},
            "ambi3": {"r": 0.2, "g": 0.1, "b": 0.3, "intensity": 0.0}
        }

    rgb_saturado = aumentar_saturacao(rgb)
    intensidade = calcular_intensidade(rgb)

    # Atualiza Avatar se ativo
    if ATUALIZAR_AVATAR:
        dados["avatar"]["r"] = round(rgb_saturado[0], 3)
        dados["avatar"]["g"] = round(rgb_saturado[1], 3)
        dados["avatar"]["b"] = round(rgb_saturado[2], 3)
        dados["avatar"]["intensity"] = round(intensidade, 3)

    # Atualiza AmbiLights se ativadas
    if ATUALIZAR_AMBI1:
        dados["ambi1"]["r"] = round(rgb_saturado[0], 3)
        dados["ambi1"]["g"] = round(rgb_saturado[1], 3)
        dados["ambi1"]["b"] = round(rgb_saturado[2], 3)
        dados["ambi1"]["intensity"] = round(intensidade, 3)

    if ATUALIZAR_AMBI2:
        dados["ambi2"]["r"] = round(rgb_saturado[0], 3)
        dados["ambi2"]["g"] = round(rgb_saturado[1], 3)
        dados["ambi2"]["b"] = round(rgb_saturado[2], 3)
        dados["ambi2"]["intensity"] = round(intensidade, 3)

    if ATUALIZAR_AMBI3:
        dados["ambi3"]["r"] = round(rgb_saturado[0], 3)
        dados["ambi3"]["g"] = round(rgb_saturado[1], 3)
        dados["ambi3"]["b"] = round(rgb_saturado[2], 3)
        dados["ambi3"]["intensity"] = round(intensidade, 3)

    with open(caminho, "w") as f:
        json.dump(dados, f, indent=2)

def main():
    carregar_configuracoes()
    ultima_cor = None
    try:
        while True:
            rgb = capturar_media_cor()
            if ultima_cor is None or cor_mudou_significativamente(rgb, ultima_cor):
                try:
                    atualizar_json(rgb)
                    ultima_cor = rgb
                except PermissionError as e:
                    print(f"[Aviso] Permissão negada ao tentar escrever no JSON: {e}")
                except Exception as e:
                    print(f"[Erro ao atualizar JSON] {e}")
            time.sleep(TIME_PER_FRAME)
    except KeyboardInterrupt:
        print("Execução interrompida pelo usuário.")
    except Exception as e:
        print(f"Erro inesperado: {e}")


if __name__ == "__main__":
    main()