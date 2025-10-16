namespace PoETest.API.Models
{
	public class LeaderboardEntry
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public int Score { get; set; }
		public DateTime Date { get; set; } = DateTime.UtcNow;
        public long TotalTimeMs { get; set; }
    }
}