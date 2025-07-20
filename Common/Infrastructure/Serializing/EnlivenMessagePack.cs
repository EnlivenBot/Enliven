using System;
using System.Buffers;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using YandexMusicResolver.Ids;

namespace Common.Infrastructure.Serializing;

public static class EnlivenMessagePack {
    public static MessagePackSerializerOptions Options { get; } = CreateResolver();

    private static MessagePackSerializerOptions CreateResolver() {
        var resolver = CompositeResolver.Create(
            [new YandexIdFormatter()],
            [
                NativeDateTimeResolver.Instance,
                BuiltinResolver.Instance,
                AttributeFormatterResolver.Instance,
                DynamicEnumResolver.Instance,
                DynamicGenericResolver.Instance,
                DynamicUnionResolver.Instance,
                DynamicObjectResolver.Instance,
                DynamicContractlessObjectResolverAllowPrivate.Instance,
                TypelessObjectResolver.Instance
            ]
        );

        return MessagePackSerializer.Typeless.DefaultOptions.WithResolver(resolver);
    }

    private class YandexIdFormatter : IMessagePackFormatter<YandexId> {
        public void Serialize(ref MessagePackWriter writer, YandexId value, MessagePackSerializerOptions options) {
            writer.Write((byte)value.IdType);
            switch (value.IdType) {
                case YandexId.YandexIdType.Long:
                    writer.Write(value.LongId);
                    break;
                case YandexId.YandexIdType.Guid:
                    writer.Write(value.GuidId.ToByteArray());
                    break;
                default:
                    throw new NotSupportedException("Current YandexId type can't be serialized");
            }
        }

        public YandexId Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) {
            var idType = (YandexId.YandexIdType)reader.ReadByte();
            return idType switch {
                YandexId.YandexIdType.Long => new YandexId(reader.ReadInt64()),
                YandexId.YandexIdType.Guid => new YandexId(new Guid(reader.ReadBytes()!.Value.ToArray())),
                _ => throw new NotSupportedException("Current YandexId type can't be deserialized")
            };
        }
    }
}