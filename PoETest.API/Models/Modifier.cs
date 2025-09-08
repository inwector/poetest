namespace PoETest.API.Models
{
    public class Modifier
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public string ModifierText { get; set; } = string.Empty;

        public Item Item { get; set; } = null!;
    }
}