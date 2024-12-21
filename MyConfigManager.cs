// MyConfigManager.cs
using System.Text.Json;

namespace MyCustomConfig
{
    public static class MyConfigManager
    {
        // Путь к JSON-файлу
        private static string ConfigPath = @"C:\server\config\my_knife_config.json";

        // Тут храним наши настройки
        public static MyKnifeConfig Config { get; private set; } = new MyKnifeConfig();

        /// <summary>
        /// Загружает (или создает) конфиг с настройками
        /// </summary>
        public static void LoadConfig()
        {
            try
            {
                // Проверка: если файла нет - создаём
                if (!File.Exists(ConfigPath))
                {
                    Console.WriteLine($"[KnifeRound] Config not found, creating default: {ConfigPath}");
                    SaveDefaultConfig();
                    return;
                }

                // Если файл есть, читаем
                string json = File.ReadAllText(ConfigPath);
                var tmpConfig = JsonSerializer.Deserialize<MyKnifeConfig>(json);
                if (tmpConfig == null)
                {
                    Console.WriteLine("[KnifeRound] Failed to parse config, using defaults.");
                    Config = new MyKnifeConfig();
                }
                else
                {
                    Config = tmpConfig;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[KnifeRound] Exception reading config: " + ex.Message);
                Console.WriteLine("[KnifeRound] Using defaults...");
                Config = new MyKnifeConfig();
            }
        }

        /// <summary>
        /// Сохраняет дефолтный конфиг
        /// </summary>
        private static void SaveDefaultConfig()
        {
            Config = new MyKnifeConfig(); // берём дефолт
            string json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
    }
}