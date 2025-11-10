using Discord;
using Discord.WebSocket;
using System.Text;

public class CombatService
{
    private readonly DatabaseService _dbService;

    public CombatService(DatabaseService dbService)
    {
        _dbService = dbService;
    }

    public async Task HandleCombatCommandAsync(SocketMessage message, ulong guildId, ulong userId)
    {
        string[] parts = message.Content.Split(' ');
        if (parts.Length < 2)
        {
            await ShowCombatOrderAsync(message.Channel, guildId, "Status Atual do Combate:");
            return;
        }

        string command = parts[1].ToLower();

        switch (command)
        {
            case "iniciar":
            case "start":
                await StartCombatAsync(message.Channel, guildId);
                break;

            case "entrar":
            case "join":
                await AddPlayerCombatantAsync(message, guildId, userId);
                break;

            case "add":
            case "adicionar":
                await AddNpcCombatantAsync(message, guildId, parts);
                break;

            case "proximo":
            case "próximo":
            case "next":
            case "prox":
                await NextTurnAsync(message.Channel, guildId);
                break;

            case "remover":
            case "remove":
                await RemoveCombatantAsync(message, guildId, parts);
                break;

            case "ordem":
            case "status":
                await ShowCombatOrderAsync(message.Channel, guildId, "Ordem de Iniciativa:");
                break;

            case "encerrar":
            case "end":
                await EndCombatAsync(message.Channel, guildId);
                break;

            default:
                await message.Channel.SendMessageAsync("Comando de combate desconhecido. Use: `iniciar`, `entrar`, `add`, `proximo`, `remover`, `ordem`, `encerrar`.");
                break;
        }
    }

    private async Task StartCombatAsync(ISocketMessageChannel channel, ulong guildId)
    {
        await _dbService.ClearCombatAsync(guildId);
        await _dbService.SetCombatStateAsync(guildId, 0, 1);

        var embed = new EmbedBuilder()
            .WithTitle("⚔️ Combate Iniciado!")
            .WithDescription("A lista de iniciativa está limpa. Jogadores, digitem `!combate entrar`.\nMestre, adicione NPCs com `!combate add [Nome] [Iniciativa]`.")
            .WithColor(Color.DarkRed)
            .Build();

        await channel.SendMessageAsync(embed: embed);
    }

    private async Task AddPlayerCombatantAsync(SocketMessage message, ulong guildId, ulong userId)
    {
        var stats = await _dbService.GetCharacterAsync(guildId, userId);
        if (stats == null)
        {
            await message.Channel.SendMessageAsync("Você precisa ter um personagem registrado (`!registrar`) para entrar no combate.");
            return;
        }

        // Regra: Iniciativa = PER + AGI (pág. 24)
        int initiative = stats["PER"] + stats["AGI"];
        string charName = (message.Author as SocketGuildUser)?.Nickname ?? message.Author.Username;

        bool added = await _dbService.AddCombatantAsync(guildId, charName, initiative, userId);
        if (!added)
        {
            await message.Channel.SendMessageAsync($"Você já está no combate, {charName}!");
            return;
        }

        await ShowCombatOrderAsync(message.Channel, guildId, $"{charName} entrou no combate com Iniciativa {initiative}!");
    }

    private async Task AddNpcCombatantAsync(SocketMessage message, ulong guildId, string[] parts)
    {
        if (parts.Length < 4 || !int.TryParse(parts.Last(), out int initiative))
        {
            await message.Channel.SendMessageAsync("Formato inválido. Use: `!combate add [Nome do NPC] [Iniciativa]` (Ex: `!combate add Invasor 8`)");
            return;
        }

        string npcName = string.Join(" ", parts.Skip(2).Take(parts.Length - 3));

        bool added = await _dbService.AddCombatantAsync(guildId, npcName, initiative, null);
        if (!added)
        {
            // Se o nome já existe, adiciona um número (Ex: Invasor 2)
            string uniqueName = $"{npcName} 2";
            int i = 3;
            while (!await _dbService.AddCombatantAsync(guildId, uniqueName, initiative, null))
            {
                uniqueName = $"{npcName} {i++}";
            }
            await ShowCombatOrderAsync(message.Channel, guildId, $"`{uniqueName}` foi adicionado ao combate com Iniciativa {initiative}!");
        }
        else
        {
            await ShowCombatOrderAsync(message.Channel, guildId, $"`{npcName}` foi adicionado ao combate com Iniciativa {initiative}!");
        }
    }

    private async Task NextTurnAsync(ISocketMessageChannel channel, ulong guildId)
    {
        var combatants = await _dbService.GetCombatantsAsync(guildId);
        if (combatants.Count == 0)
        {
            await channel.SendMessageAsync("O combate ainda não começou ou não há ninguém na ordem. Use `!combate iniciar`.");
            return;
        }

        var state = await _dbService.GetCombatStateAsync(guildId);
        int nextIndex = state.CurrentTurnIndex + 1;
        int nextRound = state.CurrentRound;

        // Se o próximo índice for o fim da lista, começa uma nova rodada
        if (nextIndex >= combatants.Count)
        {
            nextIndex = 0;
            nextRound++;
        }

        await _dbService.SetCombatStateAsync(guildId, nextIndex, nextRound);
        await ShowCombatOrderAsync(channel, guildId, $"Próximo Turno:", nextIndex, nextRound);
    }

    private async Task RemoveCombatantAsync(SocketMessage message, ulong guildId, string[] parts)
    {
        if (parts.Length < 3)
        {
            await message.Channel.SendMessageAsync("Formato inválido. Use: `!combate remover [Nome]`");
            return;
        }
        string nameToRemove = string.Join(" ", parts.Skip(2));
        bool removed = await _dbService.RemoveCombatantAsync(guildId, nameToRemove);

        if (removed)
        {
            // Reseta o turno para o topo para evitar pular alguém
            await _dbService.SetCombatStateAsync(guildId, 0, (await _dbService.GetCombatStateAsync(guildId)).CurrentRound);
            await ShowCombatOrderAsync(message.Channel, guildId, $"`{nameToRemove}` foi removido do combate.");
        }
        else
        {
            await message.Channel.SendMessageAsync($"`{nameToRemove}` não foi encontrado na ordem de combate.");
        }
    }

    private async Task EndCombatAsync(ISocketMessageChannel channel, ulong guildId)
    {
        await _dbService.ClearCombatAsync(guildId);
        var embed = new EmbedBuilder()
            .WithTitle("⚔️ Combate Encerrado")
            .WithDescription("A ordem de iniciativa foi limpa.")
            .WithColor(Color.DarkGreen)
            .Build();
        await channel.SendMessageAsync(embed: embed);
    }

    private async Task ShowCombatOrderAsync(ISocketMessageChannel channel, ulong guildId, string title, int? currentTurnIndex = null, int? currentRound = null)
    {
        var combatants = await _dbService.GetCombatantsAsync(guildId);

        if (currentTurnIndex == null || currentRound == null)
        {
            var state = await _dbService.GetCombatStateAsync(guildId);
            currentTurnIndex = state.CurrentTurnIndex;
            currentRound = state.CurrentRound;
        }

        if (combatants.Count == 0)
        {
            var embedEmpty = new EmbedBuilder()
                .WithTitle(title)
                .WithDescription("Ninguém entrou no combate ainda.\nUse `!combate entrar` ou `!combate add`.")
                .WithColor(Color.LightGrey)
                .Build();
            await channel.SendMessageAsync(embed: embedEmpty);
            return;
        }

        var sb = new StringBuilder();
        // ### CORREÇÃO DO BUG CS8130 AQUI ###
        for (int i = 0; i < combatants.Count; i++)
        {
            var combatant = combatants[i]; // Pega a tupla
            string name = combatant.Name;
            int init = combatant.Initiative;

            if (i == currentTurnIndex.Value)
            {
                sb.AppendLine($"**➡️ {i + 1}. {name} (Iniciativa: {init})**");
            }
            else
            {
                sb.AppendLine($"&nbsp;&nbsp;&nbsp;&nbsp; {i + 1}. {name} (Iniciativa: {init})");
            }
        }

        var embed = new EmbedBuilder()
            .WithTitle(title)
            .WithDescription($"**Rodada {currentRound.Value}**\n\n{sb.ToString()}")
            .WithColor(Color.Default)
            .Build();

        await channel.SendMessageAsync(embed: embed);
    }
}