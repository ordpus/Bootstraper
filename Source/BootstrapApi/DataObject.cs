using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

using BootstrapApi.Logger;

namespace Bootstrap;

public class UnityDataWrapperXml {
    [XmlElement("DataPath")]
    public string DataPath { get; set; } = null!;

    [XmlElement("Platform")]
    public string Platform { get; set; } = null!;

    [XmlElement("PersistentDataPath")]
    public string PersistentDataPath { get; set; } = null!;
}

[XmlRoot("Data")]
public class BootstrapDataXml {
    public class DllIdentifier {
        [XmlElement("Key")]
        public string Key { get; set; } = null!;

        [XmlElement("Value")]
        public string Value { get; set; } = null!;
    }

    [XmlElement("UnityData")]
    public UnityDataWrapperXml UnityData { get; set; } = null!;

    [XmlElement("DllIdentifiers")]
    public List<DllIdentifier> DllIdentifiersList { get; set; } = null!;
}

[Serializable]
[IsExternalInit]
public record UnityDataWrapper(string DataPath, string Platform, string PersistentDataPath);

[Serializable]
[IsExternalInit]
#pragma warning disable CS8907
public record BootstrapData(UnityDataWrapper UnityDataWrapper, IReadOnlyDictionary<string, string> DLLIdentifiers) {
#pragma warning restore CS8907
    public const string DataXml = "Bootstrap/data/BootstrapData.xml";
    public static readonly BootstrapData? Instance = ReadData()!;

    private readonly ImmutableDictionary<string, string> _dllIdentifiers = null!;

    public IReadOnlyDictionary<string, string> DLLIdentifiers {
        get { return _dllIdentifiers; }
        init { _dllIdentifiers = value.ToImmutableDictionary(); }
    }

    public static BootstrapData? ReadData(string filePath = DataXml) {
        if (!File.Exists(filePath)) return null;
        using var stream = new FileStream(DataXml, FileMode.Open, FileAccess.Read);
        var xml = (BootstrapDataXml)new XmlSerializer(typeof(BootstrapDataXml)).Deserialize(stream);
        var result = new BootstrapData(
            new UnityDataWrapper(xml.UnityData.DataPath, xml.UnityData.Platform, xml.UnityData.PersistentDataPath),
            ImmutableDictionary.CreateRange(xml.DllIdentifiersList.ToDictionary(x => x.Key, x => x.Value)));
        return result;
    }

    public void WriteData(string filePath = DataXml) {
        using var fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write);
        var serializer = new XmlSerializer(typeof(BootstrapDataXml));
        var xml = new BootstrapDataXml {
            UnityData = new UnityDataWrapperXml {
                DataPath = this.UnityDataWrapper.DataPath,
                Platform = this.UnityDataWrapper.Platform,
                PersistentDataPath = this.UnityDataWrapper.PersistentDataPath
            },
            DllIdentifiersList = this.DLLIdentifiers
                                     .Select(x => new BootstrapDataXml.DllIdentifier { Key = x.Key, Value = x.Value })
                                     .ToList()
        };
        serializer.Serialize(fs, xml);
    }

    public override string ToString() {
        return
            $"{nameof(BootstrapData)} {{ {nameof(UnityDataWrapper)} = {UnityDataWrapper}, {nameof(DLLIdentifiers)} = {DLLIdentifiers.ToStringSafeDictionary()} }}";
    }
}