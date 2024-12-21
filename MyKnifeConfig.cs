// MyKnifeConfig.cs
using System.Text.Json.Serialization;

namespace MyCustomConfig
{
    public class MyKnifeConfig
    {
        [JsonPropertyName("GiveArmorOnKnifeRound")]
        public int GiveArmorOnKnifeRound { get; set; } = 1;

        [JsonPropertyName("FreezeOnVote")]
        public bool FreezeOnVote { get; set; } = false;

        [JsonPropertyName("BlockTeamChangeOnVoteAndKnife")]
        public bool BlockTeamChangeOnVoteAndKnife { get; set; } = true;

        [JsonPropertyName("KnifeRoundTimer")]
        public float KnifeRoundTimer { get; set; } = 2;

        [JsonPropertyName("AfterWinningRestartXTimes")]
        public int AfterWinningRestartXTimes { get; set; } = 1;

        [JsonPropertyName("ChatDisplayName")]
        public string ChatDisplayName { get; set; } = "AVA";

        [JsonPropertyName("TeamIntroTimeKnifeStart")]
        public float TeamIntroTimeKnifeStart { get; set; } = 3;

        [JsonPropertyName("TeamIntroTimeAfterKnife")]
        public float TeamIntroTimeAfterKnife { get; set; } = 3;

        [JsonPropertyName("StartMessage")]
        public string StartMessage { get; set; } = "Ножи на готове";

        // Оставлю “опечатки” чтобы показать, как бы это выглядело
        [JsonPropertyName("VoitMessgae")]
        public string VoitMessgae { get; set; } = "Начало голосования";

        [JsonPropertyName("SwitchMeesage")]
        public string SwitchMeesage { get; set; } = "Смена стороны";

        [JsonPropertyName("StayMeesage")]
        public string StayMeesage { get; set; } = "Оставить сторону";

        // Уберём/переименуем любое свойство, которое вызывало ошибки
        // Если хочешь вечную разминку - WarmupTimeAfterKnif = 999999
        [JsonPropertyName("WarmupTimeAfterKnif")]
        public int WarmupTimeAfterKnif { get; set; } = 60;
    }
}