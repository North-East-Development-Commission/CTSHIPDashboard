namespace CTSHIPDashboard.Helpers
{
    public static class NorthEastLocationData
    {
        private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Lgas =
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Adamawa"] = new[]
                {
                    "Demsa", "Fufore", "Ganye", "Girei", "Gombi", "Guyuk", "Hong",
                    "Jada", "Lamurde", "Madagali", "Maiha", "Mayo-Belwa", "Michika",
                    "Mubi North", "Mubi South", "Numan", "Shelleng", "Song", "Toungo",
                    "Yola North", "Yola South"
                },
                ["Bauchi"] = new[]
                {
                    "Alkaleri", "Bauchi", "Bogoro", "Damban", "Darazo", "Dass", "Gamawa",
                    "Ganjuwa", "Giade", "Itas/Gadau", "Jama'are", "Katagum", "Kirfi",
                    "Misau", "Ningi", "Shira", "Tafawa Balewa", "Toro", "Warji", "Zaki"
                },
                ["Borno"] = new[]
                {
                    "Abadam", "Askira/Uba", "Bama", "Bayo", "Biu", "Chibok", "Damboa",
                    "Dikwa", "Gubio", "Guzamala", "Gwoza", "Hawul", "Jere", "Kaga",
                    "Kala/Balge", "Konduga", "Kukawa", "Kwaya Kusar", "Mafa", "Magumeri",
                    "Maiduguri Metropolitan", "Marte", "Mobbar", "Monguno", "Ngala",
                    "Nganzai", "Shani"
                },
                ["Gombe"] = new[]
                {
                    "Akko", "Balanga", "Billiri", "Dukku", "Funakaye", "Gombe",
                    "Kaltungo", "Kwami", "Nafada", "Shongom", "Yamaltu/Deba"
                },
                ["Taraba"] = new[]
                {
                    "Ardo Kola", "Bali", "Donga", "Gashaka", "Gassol", "Ibi", "Jalingo",
                    "Karim Lamido", "Kurmi", "Lau", "Sardauna", "Takum", "Ussa", "Wukari",
                    "Yorro", "Zing"
                },
                ["Yobe"] = new[]
                {
                    "Bade", "Bursari", "Damaturu", "Fika", "Fune", "Geidam", "Gujba",
                    "Gulani", "Jakusko", "Karasuwa", "Machina", "Nangere", "Nguru",
                    "Potiskum", "Tarmuwa", "Yunusari", "Yusufari"
                }
            };

        public static IReadOnlyList<string> States { get; } =
            Lgas.Keys.OrderBy(state => state).ToList();

        public static IReadOnlyList<string> GetLgas(string? state) =>
            !string.IsNullOrWhiteSpace(state) && Lgas.TryGetValue(state.Trim(), out var lgas)
                ? lgas
                : Array.Empty<string>();

        public static bool IsValidState(string? state) =>
            !string.IsNullOrWhiteSpace(state) && Lgas.ContainsKey(state.Trim());

        public static bool IsValidLga(string? state, string? lga) =>
            !string.IsNullOrWhiteSpace(lga)
            && GetLgas(state).Contains(lga.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
