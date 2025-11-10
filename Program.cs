using Discord;
using Discord.WebSocket;
using MySqlConnector;

public class Program
{
    private DiscordSocketClient _client = null!;
    private IConfiguration _config = null!;

    public static void Main(string[] args)
        => new Program().MainAsync().GetAwaiter().GetResult();

    public async Task MainAsync()
    {
        // --- MUDANÇA 1: Carregar o .env ---
        // Carrega as variáveis do .env (como DISCORD_TOKEN) para o ambiente do processo
        DotNetEnv.Env.Load();
        // ----------------------------------

        // 1. Carregar Configuração (config.json E .env)
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("config.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables(); // --- MUDANÇA 2: Adiciona as variáveis carregadas do .env ---

        _config = configBuilder.Build();

        // 2. Configurar Serviços (Injeção de Dependência)
        var services = new ServiceCollection();
        ConfigureServices(services);

        var serviceProvider = services.BuildServiceProvider();

        // 3. Iniciar o Bot
        _client = serviceProvider.GetRequiredService<DiscordSocketClient>();
        _client.Log += Log;
        _client.Ready += Client_Ready;

        _client.InteractionCreated += serviceProvider.GetRequiredService<CommandHandler>().HandleInteractionAsync;

        // --- MUDANÇA 3: Usar a variável correta do .env ---
        await _client.LoginAsync(TokenType.Bot, _config["DISCORD_TOKEN"]); // 
                                                                           // ----------------------------------------------------

        await _client.StartAsync();

        // 4. Inicializar o CommandHandler
        await serviceProvider.GetRequiredService<CommandHandler>().InitializeAsync();

        await Task.Delay(-1);
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Configura o Cliente Discord
        var discordConfig = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds |
                             GatewayIntents.GuildMembers |
                             GatewayIntents.GuildMessages |
                             GatewayIntents.MessageContent |
                             GatewayIntents.DirectMessages,
            AlwaysDownloadUsers = true
        };
        services.AddSingleton(new DiscordSocketClient(discordConfig));

        // Adiciona o Handler de Comandos
        services.AddSingleton<CommandHandler>();

        // Adiciona os Serviços de Lógica
        services.AddSingleton<CharacterService>();
        services.AddSingleton<GameService>();
        services.AddSingleton<InfoService>();
        services.AddSingleton<CombatService>();

        // Adiciona o Livro de Regras
        services.AddSingleton<Rulebook>();

        // Adiciona o Serviço de Banco de Dados
        services.AddSingleton<DatabaseService>(sp =>
        {
            var connectionStringBuilder = new MySqlConnectionStringBuilder
            {
                Server = _config["DbHost"], //
                UserID = _config["DbUser"], //
                // --- MUDANÇA 4: Usar a variável correta do .env ---
                Password = _config["DB_PASS"], // 
                // ----------------------------------------------------
                Database = _config["DbName"] //
            };
            return new DatabaseService(connectionStringBuilder.ConnectionString);
        });
    }

    private async Task Client_Ready()
    {
        Console.WriteLine($"Bot '{_client.CurrentUser.Username}' conectado e pronto!");

        // --- 5. REGISTRO DOS SLASH COMMANDS GLOBAIS ---
        var globalCommands = new ApplicationCommandProperties[]
        {
            new SlashCommandBuilder()
                .WithName("teste")
                .WithDescription("Realiza um Teste de Perícia (S.P.E.C.I.A.L. + Perícia) - pág. 13")
                .AddOption("pericia", ApplicationCommandOptionType.String, "Nome da Perícia a ser testada (Ex: armaspequenas)", isRequired: true)
                .AddOption("dificuldade", ApplicationCommandOptionType.Integer, "Dificuldade da tarefa (1-5)", isRequired: true)
                .AddOption("sorte", ApplicationCommandOptionType.Boolean, "Usar Pontos de Sorte para Cartas Marcadas (--sorte)")
                .AddOption("dados", ApplicationCommandOptionType.Integer, "Comprar dados extras (1, 2 ou 3) usando PA do grupo")
                .Build(),

            new SlashCommandBuilder()
                .WithName("npc")
                .WithDescription("Consulta a ficha de um NPC ou Criatura - pág. 332")
                .AddOption("nome", ApplicationCommandOptionType.String, "Nome do NPC/Criatura (Ex: mirelurk, invasor)", isRequired: true)
                .Build(),

            new SlashCommandBuilder()
                .WithName("regra")
                .WithDescription("Consulta uma regra, mecânica ou status do Livro de Regras")
                .AddOption("termo", ApplicationCommandOptionType.String, "Termo de busca (Ex: agonizando, cobertura, stimpak)", isRequired: true)
                .Build(),

            new SlashCommandBuilder()
                .WithName("area")
                .WithDescription("Rola a área de acerto (1d20) no combate - pág. 28")
                .Build(),
        };

        try
        {
            await _client.BulkOverwriteGlobalApplicationCommandsAsync(globalCommands);
            Console.WriteLine($"Registrei {globalCommands.Length} Slash Commands globalmente.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERRO ao registrar Slash Commands: {ex.Message}");
        }
    }

    private Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }
}