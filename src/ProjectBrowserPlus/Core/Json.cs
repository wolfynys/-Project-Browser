using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace ProjectBrowserPlus.Core
{
    /// <summary>Tiny JSON helper on top of DataContractJsonSerializer (no external dependencies).</summary>
    public static class Json
    {
        public static string Serialize<T>(T obj)
        {
            var ser = new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
            using (var ms = new MemoryStream())
            {
                ser.WriteObject(ms, obj);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        public static T Deserialize<T>(string json)
        {
            var ser = new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                return (T)ser.ReadObject(ms);
        }

        public static T Load<T>(string path) where T : class, new()
        {
            try
            {
                if (File.Exists(path)) return Deserialize<T>(File.ReadAllText(path)) ?? new T();
            }
            catch (System.Exception ex) { Log.Warn("Json load failed " + path + ": " + ex.Message); }
            return new T();
        }

        public static void Save<T>(string path, T obj)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, Serialize(obj));
            }
            catch (System.Exception ex) { Log.Warn("Json save failed " + path + ": " + ex.Message); }
        }
    }
}
