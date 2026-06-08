using System;

namespace Game.Configs
{
    /// <summary>
    /// Связывает тип конфига с именем его файла (без расширения).
    /// Пример: [ConfigFile("books")] на BookConfig → файл "books.json"
    /// в локальной папке / снапшоте / на сервере.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class ConfigFileAttribute : Attribute
    {
        public string FileName { get; }

        public ConfigFileAttribute(string fileName)
        {
            FileName = fileName;
        }
    }
}
