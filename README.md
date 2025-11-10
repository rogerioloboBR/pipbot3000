# Pip-Bot 3000 (Assistente .NET para Fallout RPG)

Este é um bot para Discord completo, construído em **.NET 8** e **C#**, projetado para atuar como um assistente de jogo e Mestre (GM) para o RPG de mesa **Fallout: O Jogo de RPG**.

O bot gerencia fichas de personagens, automatiza rolagens de dados (testes, danos, etc.), controla o combate e serve como uma rápida ferramenta de consulta ao livro de regras, tudo diretamente do Discord.

## 🚀 Tecnologias Utilizadas

* **Plataforma:** .NET 8 (Worker Service)
* **Biblioteca do Discord:** [Discord.Net](https://github.com/discord-net/Discord.Net) (v3.x)
* **Banco de Dados:** MySQL (conectado via `MySqlConnector`)
* **Configuração:** `Microsoft.Extensions.Configuration` e `DotNetEnv` (para gerenciar `config.json` e `.env`)

## ✨ Funcionalidades Principais

### 1. Criação e Gestão de Personagem
* **Assistente de Criação de PJ (`!criar-personagem`):** Um guia interativo via DM que orienta o jogador passo a passo na criação do personagem (Origem, S.P.E.C.I.A.L., Perícias, Nome) conforme as regras do livro (pág. 50).
* **Assistente de Criação de GM (`!gm-criar`):** Um assistente de DM para o Mestre criar NPCs e Criaturas customizadas (pág. 332) e salvá-los no banco de dados.
* **Registro Rápido (`!registrar`):** Um comando para registrar rapidamente um personagem apenas com o nome e atributos S.P.E.C.I.A.L..
* **Gestão de Perícias (`!pericia`):** Permite aos jogadores definir o nível de suas perícias e marcá-las como "Marcada" (Tag Skill).

### 2. Mecânicas de Jogo (GameService)
* **Testes de Perícia (`!teste` e `/teste`):** Rola 2d20 e calcula sucessos com base no `Atributo + Perícia` (pág. 13).
* **Dados de Combate (`!dano`):** Rola Dados de Combate (d6) e calcula o dano total e os efeitos conforme a pág. 29.
* **Fabricação (`!fabricar`):** Testa a fabricação de um item ou mod, comparando a Complexidade da receita com o teste de perícia (pág. 210).
* **Rerrolagem (`!rerrolar`):** Permite gastar Pontos de Sorte para rerrolar um d20 de um teste anterior (Sorte Grande, pág. 21).
* **Gerenciamento de XP (`!xp`):** Adiciona XP manualmente ou calcula o XP pela derrota de um NPC (pág. 334).

### 3. Gerenciamento de Recursos (GameService)
* **Pontos de Ação (`!pa`):** Gerencia os Pontos de Ação do grupo (máx 6), permitindo adicionar, gastar, definir ou zerar os pontos.
* **Pontos de Sorte (`!ps`):** Gerencia os Pontos de Sorte pessoais de um jogador, permitindo gastar ou redefinir (pág. 20).

### 4. Consultas ao Livro (InfoService)
* **Regras (`!regra` e `/regra`):** Busca rápida por uma regra ou mecânica no banco de dados.
* **Itens, Armas, Armaduras (`!item`):** Exibe a ficha de qualquer item do livro.
* **NPCs e Criaturas (`!npc` e `/npc`):** Exibe a ficha completa de um NPC ou criatura (pág. 332+).
* **Mods (`!mod`):** Consulta modificações de armas ou armaduras.
* **Ferimentos (`!ferimento`):** Mostra o efeito de um ferimento crítico em uma área do corpo (pág. 32).
* **Área de Acerto (`!area` e `/area`):** Rola 1d20 para determinar a área de acerto no combate (pág. 28).
* **Saque (`!vasculhar`):** Rola em tabelas de pilhagem (pág. 200+).
* **Kit do GM (`!gm`):** Mostra um painel de controle para o Mestre com os comandos mais úteis.

### 5. Gerenciamento de Combate (CombatService)
* Gerenciamento completo da ordem de iniciativa (`!combate iniciar`, `!combate encerrar`).
* Permite que PJs (`!combate entrar`) e NPCs (`!combate add`) entrem no combate.
* Calcula automaticamente a iniciativa dos PJs (PER + AGI) (pág. 24).
* Avança os turnos (`!combate proximo`) e mostra a ordem (`!combate ordem`).

## 🛠️ Configuração e Instalação

### Pré-requisitos
1.  **.NET 8 SDK:** [Download aqui](https://dotnet.microsoft.com/download/dotnet/8.0)
2.  **Servidor MySQL:** Um banco de dados local ou hospedado.
3.  **Token de Bot do Discord:** Obtido no [Portal de Desenvolvedores do Discord](https://discord.com/developers/applications).

### Passos para Executar

1.  **Clone o Repositório:**
    ```sh
    git clone [URL_DO_SEU_REPOSITORIO]
    cd [NOME_DO_PROJETO]
    ```

2.  **Restaure os Pacotes NuGet:**
    ```sh
    dotnet restore
    ```
    (O Visual Studio geralmente faz isso automaticamente ao abrir o projeto).

3.  **Crie o Banco de Dados:**
    * Crie um novo banco de dados (schema) no seu servidor MySQL (ex: `pipbot3000`).
    * Execute o script SQL (você precisará criar um) para gerar todas as tabelas necessárias (como `Personagens`, `Pericias`, `Armas`, `Regras`, etc.).
    * **Importante:** Popule as tabelas de consulta (`Armas`, `Armaduras`, `Regras`, `XP_Values`, etc.) com os dados do livro de regras.

4.  **Crie os Arquivos de Configuração:**
    * Na raiz do projeto, crie um arquivo `.env` para seus segredos:

    **.env**
    ```env
    DISCORD_TOKEN="SEU_TOKEN_AQUI"
    DB_PASS="SUA_SENHA_DO_BANCO_AQUI"
    ```

    * Na mesma pasta, crie um arquivo `config.json` para dados não-secretos:

    **config.json**
    ```json
    {
      "DbHost": "endereco_do_seu_banco",
      "DbUser": "usuario_do_banco",
      "DbName": "nome_do_banco"
    }
    ```

5.  **Configure o Visual Studio:**
    * No **Explorador de Soluções**, clique no `config.json` e no `.env`.
    * Na janela de **Propriedades**, mude **"Copiar para Diretório de Saída"** para **"Copiar se for mais recente"**.

6.  **Execute o Bot:**
    * Pressione F5 no Visual Studio ou execute o comando:
    ```sh
    dotnet run
    ```
