using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;

namespace Stakeholders.App
{
    /// <summary>JSON + gzip encoding for saves and network messages.</summary>
    public static class Codec
    {
        public static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            NullValueHandling = NullValueHandling.Ignore,
            FloatParseHandling = FloatParseHandling.Double,
            Formatting = Formatting.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Error,
        };

        public static string ToJson(object o) => JsonConvert.SerializeObject(o, Json);
        public static T FromJson<T>(string json) => JsonConvert.DeserializeObject<T>(json, Json);

        public static byte[] Compress(string text)
        {
            var raw = Encoding.UTF8.GetBytes(text);
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionLevel.Fastest, true)) gz.Write(raw, 0, raw.Length);
                return ms.ToArray();
            }
        }

        public static string Decompress(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var gz = new GZipStream(ms, CompressionMode.Decompress))
            using (var reader = new StreamReader(gz, Encoding.UTF8))
                return reader.ReadToEnd();
        }
    }

    /// <summary>One network message: a type tag and a JSON payload.</summary>
    public class NetEnvelope
    {
        public string T;
        public string J;

        public static NetEnvelope Of(string type, object payload) => new NetEnvelope { T = type, J = payload == null ? null : Codec.ToJson(payload) };
        public TPayload Read<TPayload>() => J == null ? default : Codec.FromJson<TPayload>(J);

        public byte[] Encode() => Codec.Compress(Codec.ToJson(this));
        public static NetEnvelope Decode(byte[] data) => Codec.FromJson<NetEnvelope>(Codec.Decompress(data));
    }
}
