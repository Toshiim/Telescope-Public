
namespace ShareLib.Entities
{
    public class BaseEntity<T> : IEquatable<T> where T : BaseEntity<T>
    {
        /// <summary>
        /// Идентификатор.
        /// </summary>
        public Guid Id { get; init; }

        /// <summary>
        /// Базовая сущность.
        /// </summary>
        protected BaseEntity(Guid id)
        {
            Id = id;
        }

        /// <summary>
        /// Базовая сущность.
        /// </summary>
        protected BaseEntity()
        {
            Id = Guid.NewGuid(); // Инициализация Id
        }

        /// <summary>
        /// Сравнение объекта с другим объектом этого же типа.
        /// </summary>
        /// <param name="other">Объект, с которым производится сравнение.</param>
        public bool Equals(T? other)
        {
            return other is not null && Id.Equals(other.Id);
        }

        /// <summary>
        /// Сравнение объекта с другим объектом.
        /// </summary>
        /// <param name="obj">Объект, с которым производится сравнение.</param>
        public override bool Equals(object? obj)
        {
            return obj is T other && Equals(other);
        }

        /// <summary>
        /// Получение Hash Code.
        /// </summary>
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        //Хорошие художники копируют, великие художники воруют.(c) Пикасо - Джобс - Землинский
    }

}
