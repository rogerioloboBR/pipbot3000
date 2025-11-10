using MySqlConnector;

// Assumindo que os modelos de dados (Weapon, Armor, Consumable, Mod, LootItem, Recipe, CreatureNPCStats, GMContentCreationState) 
// estão disponíveis no namespace global ou via 'using' no seu projeto.
// Nota: A definição real de GMContentCreationState e CreatureNPCStats deve estar em arquivos .cs separados.

public class DatabaseService
{
    private readonly string _connectionString;
    private const int MAX_ACTION_POINTS = 6;

    public DatabaseService(string connectionString)
    {
        _connectionString = connectionString;
    }

    #region Gerenciamento de Pontos de Ação (PA)

    public async Task<int> GetActionPointsAsync(ulong guildId)
    {
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = new MySqlCommand("SELECT CurrentPA FROM GuildActionPoints WHERE GuildId = @guildId", connection);
            command.Parameters.AddWithValue("@guildId", guildId);

            var result = await command.ExecuteScalarAsync();
            if (result != null)
            {
                return Convert.ToInt32(result);
            }
            else
            {
                await SetActionPointsAsync(guildId, 0);
                return 0;
            }
        }
    }

    public async Task SetActionPointsAsync(ulong guildId, int amount)
    {
        int clampedAmount = Math.Clamp(amount, 0, MAX_ACTION_POINTS);

        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            var command = new MySqlCommand(
                "INSERT INTO GuildActionPoints (GuildId, CurrentPA) " +
                "VALUES (@guildId, @amount) " +
                "ON DUPLICATE KEY UPDATE CurrentPA = @amount",
                connection);

            command.Parameters.AddWithValue("@guildId", guildId);
            command.Parameters.AddWithValue("@amount", clampedAmount);
            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task<int> AddActionPointsAsync(ulong guildId, int amountToAdd)
    {
        int currentPA = await GetActionPointsAsync(guildId);
        int newPA = Math.Min(currentPA + amountToAdd, MAX_ACTION_POINTS);
        await SetActionPointsAsync(guildId, newPA);
        return newPA;
    }

    public async Task<(bool Success, int CurrentPA)> SpendActionPointsAsync(ulong guildId, int amountToSpend)
    {
        int currentPA = await GetActionPointsAsync(guildId);
        if (currentPA < amountToSpend)
        {
            return (Success: false, CurrentPA: currentPA);
        }

        int newPA = currentPA - amountToSpend;
        await SetActionPointsAsync(guildId, newPA);
        return (Success: true, CurrentPA: newPA);
    }

    #endregion

    #region Gerenciamento de Personagem

    public async Task CreateCharacterAsync(ulong guildId, ulong userId, string name, int forca, int per, int res, int car, int intel, int agi, int sor)
    {
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            var command = new MySqlCommand(
                "INSERT INTO Personagens (GuildId, UserID, Nome, `FOR`, `PER`, `RES`, `CAR`, `INT`, `AGI`, `SOR`, CurrentPS, CurrentLevel, CurrentXP) " +
                "VALUES (@guildId, @userId, @name, @for, @per, @res, @car, @int, @agi, @sor, @sor, 1, 0) " +
                "ON DUPLICATE KEY UPDATE Nome = @name, `FOR` = @for, `PER` = @per, `RES` = @res, `CAR` = @car, `INT` = @int, `AGI` = @agi, `SOR` = @sor, CurrentPS = @sor",
                connection);

            command.Parameters.AddWithValue("@guildId", guildId);
            command.Parameters.AddWithValue("@userId", userId);
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@for", forca);
            command.Parameters.AddWithValue("@per", per);
            command.Parameters.AddWithValue("@res", res);
            command.Parameters.AddWithValue("@car", car);
            command.Parameters.AddWithValue("@int", intel);
            command.Parameters.AddWithValue("@agi", agi);
            command.Parameters.AddWithValue("@sor", sor);

            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task SetCharacterSkillAsync(ulong guildId, ulong userId, string skillName, int rank, bool isTag)
    {
        if (await GetCharacterAsync(guildId, userId) == null)
        {
            throw new Exception("Personagem não registrado. Use `!registrar` primeiro.");
        }

        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = new MySqlCommand(
                "INSERT INTO Pericias (GuildId, UserID, NomePericia, Rank, IsTagSkill) " +
                "VALUES (@guildId, @userId, @skillName, @rank, @isTag) " +
                "ON DUPLICATE KEY UPDATE Rank = @rank, IsTagSkill = @isTag",
                connection);

            command.Parameters.AddWithValue("@guildId", guildId);
            command.Parameters.AddWithValue("@userId", userId);
            command.Parameters.AddWithValue("@skillName", skillName);
            command.Parameters.AddWithValue("@rank", rank);
            command.Parameters.AddWithValue("@isTag", isTag);

            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task<Dictionary<string, int>?> GetCharacterAsync(ulong guildId, ulong userId)
    {
        var stats = new Dictionary<string, int>();
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            var command = new MySqlCommand("SELECT `FOR`, `PER`, `RES`, `CAR`, `INT`, `AGI`, `SOR` FROM Personagens WHERE GuildId = @guildId AND UserID = @userId", connection);
            command.Parameters.AddWithValue("@guildId", guildId);
            command.Parameters.AddWithValue("@userId", userId);

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    stats["FOR"] = reader.GetInt32("FOR");
                    stats["PER"] = reader.GetInt32("PER");
                    stats["RES"] = reader.GetInt32("RES");
                    stats["CAR"] = reader.GetInt32("CAR");
                    stats["INT"] = reader.GetInt32("INT");
                    stats["AGI"] = reader.GetInt32("AGI");
                    stats["SOR"] = reader.GetInt32("SOR");
                    return stats;
                }
            }
        }
        return null;
    }

    public async Task<(int Rank, bool IsTag)> GetCharacterSkillAsync(ulong guildId, ulong userId, string skillName)
    {
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = new MySqlCommand("SELECT Rank, IsTagSkill FROM Pericias WHERE GuildId = @guildId AND UserID = @userId AND NomePericia = @skillName", connection);
            command.Parameters.AddWithValue("@guildId", guildId);
            command.Parameters.AddWithValue("@userId", userId);
            command.Parameters.AddWithValue("@skillName", skillName);

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return (Rank: reader.GetInt32("Rank"), IsTag: reader.GetBoolean("IsTagSkill"));
                }
            }
        }
        return (Rank: 0, IsTag: false);
    }

    // NOVO: Obtém nome, ID e Nível/XP de todos os personagens no servidor
    public async Task<List<(string Name, ulong UserId, int Level, int XP)>> GetAllCharactersInGuildAsync(ulong guildId)
    {
        var characters = new List<(string, ulong, int, int)>();
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = new MySqlCommand("SELECT Nome, UserID, CurrentLevel, CurrentXP FROM Personagens WHERE GuildId = @guildId", connection);
            command.Parameters.AddWithValue("@guildId", guildId);

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    characters.Add((
                        reader.GetString("Nome"),
                        reader.GetUInt64("UserID"),
                        reader.GetInt32("CurrentLevel"),
                        reader.GetInt32("CurrentXP")
                    ));
                }
            }
        }
        return characters;
    }

    // NOVO: Obtém apenas o nome do personagem
    public async Task<string?> GetCharacterNameAsync(ulong guildId, ulong userId)
    {
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = new MySqlCommand("SELECT Nome FROM Personagens WHERE GuildId = @guildId AND UserID = @userId", connection);
            command.Parameters.AddWithValue("@guildId", guildId);
            command.Parameters.AddWithValue("@userId", userId);

            var result = await command.ExecuteScalarAsync();
            return result?.ToString();
        }
    }

    #endregion

    #region Gerenciamento de Pontos de Sorte (PS)

    public async Task<(int Current, int Max)> GetLuckPointsAsync(ulong guildId, ulong userId)
    {
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = new MySqlCommand("SELECT CurrentPS, `SOR` FROM Personagens WHERE GuildId = @guildId AND UserID = @userId", connection);
            command.Parameters.AddWithValue("@guildId", guildId);
            command.Parameters.AddWithValue("@userId", userId);

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return (Current: reader.GetInt32("CurrentPS"), Max: reader.GetInt32("SOR"));
                }
            }
        }
        return (Current: 0, Max: 0);
    }

    public async Task<int> SetLuckPointsAsync(ulong guildId, ulong userId, int amount)
    {
        var (current, max) = await GetLuckPointsAsync(guildId, userId);
        if (max == 0) throw new Exception("Personagem não encontrado.");

        int clampedAmount = Math.Clamp(amount, 0, max);

        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = new MySqlCommand("UPDATE Personagens SET CurrentPS = @amount WHERE GuildId = @guildId AND UserID = @userId", connection);
            command.Parameters.AddWithValue("@guildId", guildId);
            command.Parameters.AddWithValue("@userId", userId);
            command.Parameters.AddWithValue("@amount", clampedAmount);
            await command.ExecuteNonQueryAsync();
        }
        return clampedAmount;
    }

    public async Task<(bool Success, int CurrentPS)> SpendLuckPointsAsync(ulong guildId, ulong userId, int amountToSpend)
    {
        var (current, max) = await GetLuckPointsAsync(guildId, userId);
        if (current < amountToSpend)
        {
            return (Success: false, CurrentPS: current);
        }

        int newPS = current - amountToSpend;
        await SetLuckPointsAsync(guildId, userId, newPS);
        return (Success: true, CurrentPS: newPS);
    }

    #endregion

    #region Gerenciamento de XP e Nível

    /// <summary>
    /// Calcula o XP total necessário para o próximo nível (pág. 49).
    /// </summary>
    private int GetRequiredXPForNextLevel(int currentLevel)
    {
        var xpMap = new Dictionary<int, int> {
            {1, 100}, {2, 300}, {3, 600}, {4, 1000}, {5, 1500},
            {6, 2100}, {7, 2800}, {8, 3600}, {9, 4500}, {10, 5500},
            {11, 6600}, {12, 7800}, {13, 9100}, {14, 10500}, {15, 12000},
            {16, 13600}, {17, 15300}, {18, 17100}, {19, 19000}
        };

        if (currentLevel >= 20)
        {
            // Regra simplificada para níveis acima de 20
            return (currentLevel + 1) * 100;
        }

        if (xpMap.TryGetValue(currentLevel, out int xpRequiredToReachNextLevelTotal))
        {
            int xpRequiredToReachCurrentLevelTotal = xpMap.GetValueOrDefault(currentLevel - 1, 0);
            return xpRequiredToReachNextLevelTotal - xpRequiredToReachCurrentLevelTotal;
        }

        return 0;
    }

    /// <summary>
    /// Adiciona XP ao personagem e verifica se houve aumento de nível.
    /// </summary>
    public async Task<(int newXP, int newLevel, int xpToNextLevel)> AddXPToCharacter(ulong guildId, ulong userId, int xpAmount)
    {
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var selectCommand = new MySqlCommand("SELECT CurrentXP, CurrentLevel, `SOR`, `INT` FROM Personagens WHERE GuildId = @guildId AND UserID = @userId", connection);
            selectCommand.Parameters.AddWithValue("@guildId", guildId);
            selectCommand.Parameters.AddWithValue("@userId", userId);

            using (var reader = await selectCommand.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync()) throw new Exception("Personagem não encontrado.");

                int currentXP = reader.GetInt32("CurrentXP");
                int currentLevel = reader.GetInt32("CurrentLevel");
                int newXP = currentXP + xpAmount;
                int newLevel = currentLevel;

                int requiredXP;

                // Loop para verificar se houve múltiplos aumentos de nível
                do
                {
                    requiredXP = GetRequiredXPForNextLevel(newLevel);

                    if (requiredXP > 0 && newXP >= requiredXP)
                    {
                        newXP -= requiredXP;
                        newLevel++;
                    }
                } while (requiredXP > 0 && newXP >= requiredXP);

                // Salvar novo estado
                reader.Close();
                var updateCommand = new MySqlCommand("UPDATE Personagens SET CurrentXP = @newXP, CurrentLevel = @newLevel WHERE GuildId = @guildId AND UserID = @userId", connection);
                updateCommand.Parameters.AddWithValue("@newXP", newXP);
                updateCommand.Parameters.AddWithValue("@newLevel", newLevel);

                // ADICIONE ESTAS DUAS LINHAS:
                updateCommand.Parameters.AddWithValue("@guildId", guildId);
                updateCommand.Parameters.AddWithValue("@userId", userId);

                await updateCommand.ExecuteNonQueryAsync();

                return (newXP, newLevel, GetRequiredXPForNextLevel(newLevel) - newXP);
            }
        }
    }

    /// <summary>
    /// Obtém o valor de XP concedido por um NPC, baseado no seu Nível e Tipo.
    /// </summary>
    public async Task<int> GetXPAmountForNPC(CreatureNPCStats stats)
    {
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = new MySqlCommand("SELECT * FROM XP_Values WHERE Nivel = @Nivel", connection);
            command.Parameters.AddWithValue("@Nivel", stats.Nivel);

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return stats.Tipo switch
                    {
                        "Notável" => reader.GetInt32("XP_Notavel_Poderoso"),
                        "Principal" => reader.GetInt32("XP_Principal_Lendario"),
                        _ => reader.GetInt32("XP_Normal")
                    };
                }
            }
        }
        return 0;
    }

    #endregion


    #region Gerenciamento de Informações (Regras, Ferimentos, Itens, Loot, Craft, NPC)

    public async Task<(string Title, string Text, int Page)> GetRuleAsync(string query)
    {
        // ... (Corpo do método) ...
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = new MySqlCommand("SELECT Titulo, Texto, Pagina FROM Regras WHERE Chave LIKE @query LIMIT 1", connection);
            command.Parameters.AddWithValue("@query", $"%{query}%");

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return (
                        Title: reader.GetString("Titulo") ?? string.Empty,
                        Text: reader.GetString("Texto") ?? string.Empty,
                        Page: reader.GetInt32("Pagina")
                    );
                }
            }
        }
        return (null, null, 0);
    }

    public async Task<(string Title, string Text, int Page)> GetInjuryAsync(string locationKey)
    {
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = new MySqlCommand("SELECT Titulo, Texto, Pagina FROM Ferimentos WHERE Chave = @locationKey LIMIT 1", connection);
            command.Parameters.AddWithValue("@locationKey", locationKey);

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return (
                        Title: reader.GetString("Titulo") ?? string.Empty,
                        Text: reader.GetString("Texto") ?? string.Empty,
                        Page: reader.GetInt32("Pagina")
                    );
                }
            }
        }
        return (null, null, 0);
    }

    public async Task<Weapon?> GetWeaponAsync(string nameQuery)
    {
        // ... (Corpo do método) ...
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = new MySqlCommand("SELECT * FROM Armas WHERE Nome LIKE @query LIMIT 1", connection);
            command.Parameters.AddWithValue("@query", $"%{nameQuery}%");

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return new Weapon
                    {
                        Nome = reader.GetString("Nome"),
                        Pericia = reader.GetString("Pericia"),
                        Dano = reader.GetString("Dano"),
                        Efeitos = reader.GetString("Efeitos"),
                        TipoDano = reader.GetString("TipoDano"),
                        CdT = reader.GetInt32("CdT"),
                        Distancia = reader.GetString("Distancia"),
                        Qualidades = reader.GetString("Qualidades"),
                        Peso = reader.GetDouble("Peso"),
                        Custo = reader.GetInt32("Custo"),
                        Raridade = reader.GetInt32("Raridade")
                    };
                }
            }
        }
        return null;
    }

    public async Task<Armor?> GetArmorAsync(string nameQuery)
    {
        // ... (Corpo do método) ...
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = new MySqlCommand("SELECT * FROM Armaduras WHERE Nome LIKE @query LIMIT 1", connection);
            command.Parameters.AddWithValue("@query", $"%{nameQuery}%");

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return new Armor
                    {
                        Nome = reader.GetString("Nome"),
                        AreaCoberta = reader.GetString("AreaCoberta"),
                        RD_Fisico = reader.GetInt32("RD_Fisico"),
                        RD_Energetico = reader.GetInt32("RD_Energetico"),
                        RD_Radiativo = reader.GetInt32("RD_Radiativo"),
                        PV = reader.IsDBNull(reader.GetOrdinal("PV")) ? (int?)null : reader.GetInt32("PV"),
                        Peso = reader.GetDouble("Peso"),
                        Custo = reader.GetInt32("Custo"),
                        Raridade = reader.GetInt32("Raridade")
                    };
                }
            }
        }
        return null;
    }

    public async Task<Consumable?> GetConsumableAsync(string nameQuery)
    {
        // ... (Corpo do método) ...
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = new MySqlCommand("SELECT * FROM Consumiveis WHERE Nome LIKE @query LIMIT 1", connection);
            command.Parameters.AddWithValue("@query", $"%{nameQuery}%");

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return new Consumable
                    {
                        Nome = reader.GetString("Nome"),
                        PV_Curado = reader.GetString("PV_Curado"),
                        Efeito = reader.GetString("Efeito"),
                        Irradiado = reader.GetString("Irradiado"),
                        Peso = reader.GetDouble("Peso"),
                        Custo = reader.GetInt32("Custo"),
                        Raridade = reader.GetInt32("Raridade")
                    };
                }
            }
        }
        return null;
    }

    public async Task<Mod?> GetModAsync(string nameQuery)
    {
        // ... (Corpo do método) ...
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = new MySqlCommand("SELECT * FROM Mods WHERE Nome LIKE @query LIMIT 1", connection);
            command.Parameters.AddWithValue("@query", $"%{nameQuery}%");

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return new Mod
                    {
                        Nome = reader.GetString("Nome"),
                        Efeitos = reader.GetString("Efeitos"),
                        PesoMod = reader.GetDouble("PesoMod"),
                        CustoMod = reader.GetInt32("CustoMod"),
                        Vantagens = reader.GetString("Vantagens"),
                        Pericia = reader.GetString("Pericia"),
                        Raridade = reader.GetString("Raridade"),
                        TipoMod = reader.GetString("TipoMod"),
                        FontePagina = reader.GetInt32("FontePagina")
                    };
                }
            }
        }
        return null;
    }

    public async Task<LootItem?> GetLootResultAsync(string tableName, int rollValue)
    {
        // ... (Corpo do método) ...
        var allowedTables = new HashSet<string>
        {
            "Loot_Municao", "Loot_Armadura", "Loot_Roupa", "Loot_Comida",
            "Loot_Bebida", "Loot_Narco", "Loot_Arma_Distancia", "Loot_Arma_CorpoACorpo",
            "Loot_Explosivos", "Loot_Curiosidades", "Loot_Forrageamento", "Loot_NukaCola"
        };

        if (!allowedTables.Contains(tableName))
        {
            throw new ArgumentException("Nome de tabela de pilhagem inválido.");
        }

        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            var command = new MySqlCommand($"SELECT ItemName, Quantidade FROM {tableName} WHERE RollValue = @rollValue", connection);
            command.Parameters.AddWithValue("@rollValue", rollValue);

            if (tableName == "Loot_Armadura" || tableName == "Loot_Roupa" || tableName == "Loot_Comida" ||
                tableName == "Loot_Bebida" || tableName == "Loot_Narco" || tableName == "Loot_Arma_Distancia" ||
                tableName == "Loot_Arma_CorpoACorpo" || tableName == "Loot_Curiosidades" ||
                tableName == "Loot_Forrageamento" || tableName == "Loot_NukaCola")
            {
                command.CommandText = $"SELECT ItemName, '1' AS Quantidade FROM {tableName} WHERE RollValue = @rollValue";
            }

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return new LootItem
                    {
                        ItemName = reader.GetString("ItemName"),
                        Quantidade = reader.GetString("Quantidade")
                    };
                }
            }
        }
        return null;
    }

    public async Task<Recipe?> GetRecipeAsync(string nameQuery)
    {
        // ... (Corpo do método) ...
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            // 1. Tenta buscar na tabela 'Receitas'
            var recipeCommand = new MySqlCommand("SELECT * FROM Receitas WHERE ItemNome LIKE @query LIMIT 1", connection);
            recipeCommand.Parameters.AddWithValue("@query", $"%{nameQuery}%");

            using (var reader = await recipeCommand.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return new Recipe
                    {
                        ItemName = reader.GetString("ItemNome"),
                        Materiais = reader.GetString("Materiais"),
                        Complexidade = reader.GetInt32("Complexidade"),
                        Vantagens = reader.GetString("Vantagens"),
                        Pericia = reader.GetString("Pericia"),
                        Raridade = reader.GetString("Raridade"),
                        FontePagina = reader.GetInt32("FontePagina"),
                        IsMod = false
                    };
                }
            }

            // 2. Se não encontrou, tenta buscar na tabela 'Mods'
            var modCommand = new MySqlCommand("SELECT * FROM Mods WHERE Nome LIKE @query LIMIT 1", connection);
            modCommand.Parameters.AddWithValue("@query", $"%{nameQuery}%");

            using (var reader = await modCommand.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return new Recipe
                    {
                        ItemName = reader.GetString("Nome"),
                        Materiais = null,
                        Complexidade = reader.GetInt32("Complexidade"),
                        Vantagens = reader.GetString("Vantagens"),
                        Pericia = reader.GetString("Pericia"),
                        Raridade = reader.GetString("Raridade"),
                        FontePagina = reader.GetInt32("FontePagina"),
                        IsMod = true
                    };
                }
            }
        }
        return null;
    }

    public async Task<CreatureNPCStats?> GetCreatureStatsAsync(string nameQuery)
    {
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = new MySqlCommand("SELECT * FROM Creature_NPC_Stats WHERE Nome LIKE @query LIMIT 1", connection);
            command.Parameters.AddWithValue("@query", $"%{nameQuery}%");

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return new CreatureNPCStats
                    {
                        Nome = reader.GetString("Nome"),
                        Nivel = reader.GetInt32("Nivel"),
                        Tipo = reader.GetString("Tipo"),
                        PalavrasChave = reader.GetString("PalavrasChave"),
                        FOR_Val = reader.GetInt32("FOR_Val"),
                        PER_Val = reader.GetInt32("PER_Val"),
                        RES_Val = reader.GetInt32("RES_Val"),
                        CAR_Val = reader.GetInt32("CAR_Val"),
                        INT_Val = reader.GetInt32("INT_Val"),
                        AGI_Val = reader.GetInt32("AGI_Val"),
                        SOR_Val = reader.GetInt32("SOR_Val"),
                        PV_Base = reader.GetInt32("PV_Base"),
                        Iniciativa_Base = reader.GetInt32("Iniciativa_Base"),
                        Defesa = reader.GetInt32("Defesa"),
                        RD_Fisico_Base = reader.GetInt32("RD_Fisico_Base"),
                        RD_Energetico_Base = reader.GetInt32("RD_Energetico_Base"),
                        RD_Radiativo_Base = reader.GetString("RD_Radiativo_Base"),
                        RD_Venenoso_Base = reader.GetString("RD_Venenoso_Base"),
                        Ataques = reader.GetString("Ataques"),
                        Inventario = reader.GetString("Inventario"),
                        Habilidades_Especiais = reader.GetString("Habilidades_Especiais"),
                        FontePagina = reader.GetInt32("FontePagina")
                    };
                }
            }
        }
        return null;
    }

    #endregion

    #region Gerenciamento de Combate

    public async Task<bool> AddCombatantAsync(ulong guildId, string name, int initiative, ulong? userId)
    {
        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var command = new MySqlCommand(
                    "INSERT INTO Combat_Tracker (GuildId, CharacterName, Initiative, UserID) " +
                    "VALUES (@guildId, @name, @initiative, @userId)",
                    connection);

                command.Parameters.AddWithValue("@guildId", guildId);
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@initiative", initiative);
                command.Parameters.AddWithValue("@userId", (object?)userId ?? DBNull.Value);

                await command.ExecuteNonQueryAsync();
                return true;
            }
        }
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
        {
            return false;
        }
    }

    public async Task<bool> RemoveCombatantAsync(ulong guildId, string name)
    {
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = new MySqlCommand(
                "DELETE FROM Combat_Tracker WHERE GuildId = @guildId AND CharacterName = @name",
                connection);

            command.Parameters.AddWithValue("@guildId", guildId);
            command.Parameters.AddWithValue("@name", name);

            int rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
    }

    public async Task ClearCombatAsync(ulong guildId)
    {
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = new MySqlCommand("DELETE FROM Combat_Tracker WHERE GuildId = @guildId", connection);
            command.Parameters.AddWithValue("@guildId", guildId);
            await command.ExecuteNonQueryAsync();

            var stateCommand = new MySqlCommand(
                "INSERT INTO Combat_State (GuildId, CurrentTurnIndex, CurrentRound) VALUES (@guildId, 0, 1) " +
                "ON DUPLICATE KEY UPDATE CurrentTurnIndex = 0, CurrentRound = 1",
                connection);
            stateCommand.Parameters.AddWithValue("@guildId", guildId);
            await stateCommand.ExecuteNonQueryAsync();
        }
    }

    public async Task<List<(string Name, int Initiative)>> GetCombatantsAsync(ulong guildId)
    {
        var combatants = new List<(string, int)>();
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = new MySqlCommand(
                "SELECT CharacterName, Initiative FROM Combat_Tracker WHERE GuildId = @guildId ORDER BY Initiative DESC, CharacterName",
                connection);
            command.Parameters.AddWithValue("@guildId", guildId);

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    combatants.Add((reader.GetString("CharacterName"), reader.GetInt32("Initiative")));
                }
            }
        }
        return combatants;
    }

    public async Task<(int CurrentTurnIndex, int CurrentRound)> GetCombatStateAsync(ulong guildId)
    {
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = new MySqlCommand("SELECT CurrentTurnIndex, CurrentRound FROM Combat_State WHERE GuildId = @guildId", connection);
            command.Parameters.AddWithValue("@guildId", guildId);

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return (reader.GetInt32("CurrentTurnIndex"), reader.GetInt32("CurrentRound"));
                }
            }
        }
        await SetCombatStateAsync(guildId, 0, 1);
        return (0, 1);
    }

    public async Task SetCombatStateAsync(ulong guildId, int turnIndex, int round)
    {
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = new MySqlCommand(
                "INSERT INTO Combat_State (GuildId, CurrentTurnIndex, CurrentRound) VALUES (@guildId, @index, @round) " +
                "ON DUPLICATE KEY UPDATE CurrentTurnIndex = @index, CurrentRound = @round",
                connection);

            command.Parameters.AddWithValue("@guildId", guildId);
            command.Parameters.AddWithValue("@index", turnIndex);
            command.Parameters.AddWithValue("@round", round);
            await command.ExecuteNonQueryAsync();
        }
    }

    #endregion

    // NOVO: Método para salvar NPC/Criatura customizado pelo GM
    public async Task SaveCreatureNPCStatsAsync(GMContentCreationState state, CreatureNPCStats calculatedStats)
    {
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            // Usamos REPLACE para garantir que um NPC com o mesmo nome seja atualizado.
            var command = new MySqlCommand(
                "REPLACE INTO Creature_NPC_Stats (Nome, Nivel, Tipo, PalavrasChave, " +
                "`FOR_Val`, `PER_Val`, `RES_Val`, `CAR_Val`, `INT_Val`, `AGI_Val`, `SOR_Val`, " +
                "PV_Base, Iniciativa_Base, Defesa, RD_Fisico_Base, RD_Energetico_Base, " +
                "RD_Radiativo_Base, RD_Venenoso_Base, Ataques, Inventario, Habilidades_Especiais, FontePagina) " +
                "VALUES (@Nome, @Nivel, @Tipo, @Keywords, " +
                "@FOR, @PER, @RES, @CAR, @INT, @AGI, @SOR, " +
                "@PV, @INIT, @DEF, @RDF, @RDE, @RDR, @RDV, @Ataques, @Inventario, @Habilidades, @Pagina)",
                connection);

            // Parâmetros do State
            command.Parameters.AddWithValue("@Nome", state.Name);
            command.Parameters.AddWithValue("@Nivel", state.Level);
            command.Parameters.AddWithValue("@Tipo", state.Type);
            command.Parameters.AddWithValue("@Keywords", string.Join(", ", state.Keywords));

            // Parâmetros de Atributos
            command.Parameters.AddWithValue("@FOR", state.Attributes["FOR"]);
            command.Parameters.AddWithValue("@PER", state.Attributes["PER"]);
            command.Parameters.AddWithValue("@RES", state.Attributes["RES"]);
            command.Parameters.AddWithValue("@CAR", state.Attributes["CAR"]);
            command.Parameters.AddWithValue("@INT", state.Attributes["INT"]);
            command.Parameters.AddWithValue("@AGI", state.Attributes["AGI"]);
            command.Parameters.AddWithValue("@SOR", state.Attributes["SOR"]);

            // Parâmetros de Estatísticas Calculadas
            command.Parameters.AddWithValue("@PV", calculatedStats.PV_Base);
            command.Parameters.AddWithValue("@INIT", calculatedStats.Iniciativa_Base);
            command.Parameters.AddWithValue("@DEF", calculatedStats.Defesa);
            command.Parameters.AddWithValue("@RDF", calculatedStats.RD_Fisico_Base);
            command.Parameters.AddWithValue("@RDE", calculatedStats.RD_Energetico_Base);
            command.Parameters.AddWithValue("@RDR", calculatedStats.RD_Radiativo_Base);
            command.Parameters.AddWithValue("@RDV", calculatedStats.RD_Venenoso_Base);

            // Parâmetros de Campos Complexos (Vindos da Criação)
            command.Parameters.AddWithValue("@Ataques", calculatedStats.Ataques);
            command.Parameters.AddWithValue("@Inventario", calculatedStats.Inventario);
            command.Parameters.AddWithValue("@Habilidades", calculatedStats.Habilidades_Especiais);
            command.Parameters.AddWithValue("@Pagina", calculatedStats.FontePagina);

            await command.ExecuteNonQueryAsync();
        }
    }
}