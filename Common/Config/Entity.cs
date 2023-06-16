namespace Common.Config {
    public class Entity {
        public Entity() { }

        public Entity(string value) {
            Value = value;
        }

        public Entity(object value) {
            Value = value.ToString();
        }

        public object? Id { get; set; }
        public string? Value { get; set; }

        public static implicit operator string?(Entity? v) {
            return v?.Value;
        }
    }
}