using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace GodGame.Save
{
    /// <summary>
    /// Serialización JSON del guardado con Newtonsoft.Json (paquete com.unity.nuget.newtonsoft-json en Unity,
    /// NuGet Newtonsoft.Json en dotnet). Solo se escriben campos públicos de instancia, igual que hacía
    /// JsonUtility: las propiedades calculadas (HasOwner, Exists…) nunca llegan al archivo.
    /// TypeNameHandling.None: el JSON no puede pedir instanciar tipos arbitrarios.
    /// </summary>
    public static class SaveJson
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Formatting = Formatting.None,
            ContractResolver = new PublicFieldsContractResolver()
        };

        public static string ToJson(object value) => JsonConvert.SerializeObject(value, Settings);

        public static T FromJson<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings);
    }

    /// <summary>Resolver que expone solo los campos públicos de instancia (sin propiedades).</summary>
    public sealed class PublicFieldsContractResolver : DefaultContractResolver
    {
        protected override List<MemberInfo> GetSerializableMembers(Type objectType)
        {
            return objectType
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Cast<MemberInfo>()
                .ToList();
        }
    }
}
