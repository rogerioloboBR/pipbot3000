# 🤖 Pip-Bot 3000 (Assistente .NET para Fallout RPG)

> **PROPRIEDADE DA VAULT-TEC | TERMINAL DO SUPERVISOR**
> **ASSUNTO:** Relatório de Automação - Projeto PIP-BOT 3000
> **STATUS:** <span style="color:lime;">OPERACIONAL</span>
> 
> "A guerra... a guerra nunca muda."

Este é um assistente de bot para Discord completo, construído em **.NET 8** e **C#**, projetado para atuar como um assistente de jogo e Mestre (GM) para o RPG de mesa **Fallout: O Jogo de RPG**.

O bot gerencia fichas de personagens, automatiza rolagens de dados, controla o combate e serve como uma rápida ferramenta de consulta ao livro de regras, tudo diretamente do Discord.

## ⚙️ [ ESPECIFICAÇÕES TÉCNICAS ]

* **Plataforma de Núcleo:** .NET 8 (Worker Service)
* **Biblioteca de Interface (Discord):** Discord.Net (v3.x)
* **Unidade de Persistência (Banco de Dados):** MySQL (conectado via `MySqlConnector`)
* **Protocolo de Segurança (Config):** `DotNetEnv` (para `.env`) e `Microsoft.Extensions.Configuration` (para `config.json`)

## ☢️ [ MÓDULOS DE SOFTWARE OPERACIONAIS ]

### 1. Módulo de Aquisição de Sujeito (CharacterService)
* **Assistente de Criação de PJ (`!criar-personagem`):** Um guia interativo via DM que orienta o jogador passo a passo na criação do personagem (Origem, S.P.E.C.I.A.L., Perícias, Nome) conforme as regras do livro (pág. 50).
* **Assistente de Criação de GM (`!gm-criar`):** Um assistente de DM para o Mestre criar NPCs e Criaturas customizadas (pág. 332) e salvá-los no banco de dados.
* **Registro Rápido (`!registrar`):** Um comando para registrar rapidamente um personagem apenas com o nome e atributos S.P.E.C.I.A.L. (soma 40).
* **Gestão de Perícias (`!pericia`):** Permite aos jogadores definir o nível de suas perícias e marcá-las como "Marcada" (Tag Skill).

### 2. Módulo de Simulação de Eventos (GameService)
* **Testes de Perícia (`!teste` e `/teste`):** Rola 2d20 e calcula sucessos com base no `Atributo + Perícia` (pág. 13).
* **Dados de Combate (`!dano`):** Rola Dados de Combate (d6) e calcula o dano total e os efeitos conforme a pág. 29.
* **Fabricação (`!fabricar`):** Testa a fabricação de um item ou mod, comparando a Complexidade da receita com o teste de perícia (pág. 210).
* **Rerrolagem (`!rerrolar`):** Permite gastar Pontos de Sorte para rerrolar um d20 de um teste anterior (Sorte Grande, pág. 21).
* **Gerenciamento de XP (`!xp`):** Adiciona XP manualmente ou calcula o XP pela derrota de um NPC (pág. 334).

### 3. Módulo de Gerenciamento de Recursos (GameService)
* **Pontos de Ação (`!pa`):** Gerencia os Pontos de Ação do grupo (máx 6), permitindo adicionar, gastar, definir ou zerar os pontos.
* **Pontos de Sorte (`!ps`):** Gerencia os Pontos de Sorte pessoais de um jogador, permitindo gastar ou redefinir (pág. 20).

### 4. Módulo de Banco de Dados (InfoService)
* **Regras (`!regra` e `/regra`):** Busca rápida por uma regra ou mecânica.
* **Itens, Armas, Armaduras (`!item`):** Exibe a ficha de qualquer item do livro.
* **NPCs e Criaturas (`!npc` e `/npc`):** Exibe a ficha completa de um NPC ou criatura (pág. 332+).
* **Ferimentos (`!ferimento`):** Mostra o efeito de um ferimento crítico (pág. 32).
* **Área de Acerto (`!area` e `/area`):** Rola 1d20 para determinar a área de acerto (pág. 28).
* **Saque (`!vasculhar`):** Rola em tabelas de pilhagem (pág. 200+).
* **Kit do GM (`!gm`):** Mostra um painel de controle para o Mestre.

### 5. Módulo de Assistência Tática (CombatService)
* Gerenciamento completo da ordem de iniciativa (`!combate iniciar`, `!combate encerrar`).
* Permite que PJs (`!combate entrar`) e NPCs (`!combate add`) entrem no combate.
* Calcula automaticamente a iniciativa dos PJs (PER + AGI) (pág. 24).
* Avança os turnos (`!combate proximo`) e mostra a ordem (`!combate ordem`).

## 🛠️ [ PROTOCOLO DE ATIVAÇÃO DO SUPERVISOR ]

Siga estas etapas para inicializar sua própria instância do Pip-Bot 3000.

### Pré-requisitos
1.  **.NET 8 SDK** (Instalado)
2.  **Servidor MySQL** (Local ou Hospedado)
3.  **Token de Bot do Discord** (Obtido no [Portal de Desenvolvedores do Discord](https://discord.com/developers/applications))

### Passos para Executar

1.  **Adquirir os Esquemas (Clone o Repositório):**
    ```sh
    git clone [URL_DO_SEU_REPOSITORIO]
    cd [NOME_DO_PROJETO]
    ```

2.  **Compilar Dependências (Restore NuGet):**
    ```sh
    dotnet restore
    ```
    *(O Visual Studio faz isso automaticamente ao abrir o projeto).*

3.  **Inicializar o Banco de Dados:**
    * Crie um novo banco de dados (schema) no seu servidor MySQL (ex: `pipbot3000`).
    * Execute o script SQL fornecido (ou crie um baseado no `DatabaseService.cs`) para gerar todas as tabelas.
    * **Importante:** Você deve popular as tabelas de consulta (`Armas`, `Armaduras`, `Regras`, `XP_Values`, `Loot_Municao`, etc.) com os dados do livro de regras para que os comandos de consulta funcionem.

4.  **Configurar Arquivos de Inicialização:**
    * Na raiz do projeto, crie um arquivo `.env` para seus segredos:

    **.env**
    ```env
    DISCORD_TOKEN="SEU_TOKEN_DO_BOT_AQUI"
    DB_PASS="SUA_SENHA_DO_BANCO_AQUI"
    ```

    * Na mesma pasta, crie (ou edite) o arquivo `config.json` para dados não-secretos:

    **config.json**
    ```json
    {
      "DbHost": "endereco_do_seu_banco",
      "DbUser": "usuario_do_banco",
      "DbName": "nome_do_banco"
    }
    ```

5.  **Preparar o Compilador (Visual Studio):**
    * No **Explorador de Soluções**, clique no `config.json` e no `.env`.
    * Na janela de **Propriedades**, mude **"Copiar para Diretório de Saída"** para **"Copiar se for mais recente"**. (Este passo é crucial!)

6.  **Executar Ativação:**
    * Pressione F5 no Visual Studio ou execute o comando:
    ```sh
    dotnet run
    ```

> ... FIM DO RELATÓRIO ...
