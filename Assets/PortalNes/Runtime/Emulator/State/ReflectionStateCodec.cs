using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using PortalNes.Emulator.Apu;
using PortalNes.Emulator.Bus;
using PortalNes.Emulator.Cpu;
using PortalNes.Emulator.Input;
using PortalNes.Emulator.Mappers;
using PortalNes.Emulator.Ppu;

namespace PortalNes.Emulator.State
{
    internal static class ReflectionStateCodec
    {
        public static byte[] Capture(object target)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                WriteObject(writer, target, target.GetType());
                return stream.ToArray();
            }
        }

        public static void Restore(object target, byte[] data)
        {
            if (target == null || data == null) throw new ArgumentNullException();
            using (var stream = new MemoryStream(data, false))
            using (var reader = new BinaryReader(stream))
                ReadObject(reader, target, target.GetType());
        }

        private static void WriteObject(BinaryWriter writer, object value, Type type)
        {
            if (!type.IsValueType)
            {
                writer.Write(value != null);
                if (value == null) return;
            }
            List<FieldInfo> fields = Fields(type);
            writer.Write(fields.Count);
            foreach (FieldInfo field in fields)
            {
                writer.Write(FieldKey(field));
                WriteValue(writer, field.GetValue(value), field.FieldType);
            }
        }

        private static void ReadObject(BinaryReader reader, object target, Type type)
        {
            if (!type.IsValueType && !reader.ReadBoolean())
                throw new InvalidDataException($"Save-state object '{type.Name}' is missing.");
            var fields = new Dictionary<string, FieldInfo>();
            foreach (FieldInfo field in Fields(type)) fields[FieldKey(field)] = field;
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string name = reader.ReadString();
                if (!fields.TryGetValue(name, out FieldInfo field))
                    throw new InvalidDataException($"Save-state field '{type.Name}.{name}' is not available.");
                object current = field.GetValue(target);
                object restored = ReadValue(reader, field.FieldType, current);
                // ReadValue mutates a boxed struct in place. The box must still
                // be assigned back to its parent field; reference comparison
                // alone incorrectly treats it as unchanged.
                if (!field.IsInitOnly &&
                    (field.FieldType.IsValueType || !ReferenceEquals(restored, current)))
                    field.SetValue(target, restored);
            }
        }

        private static void WriteValue(BinaryWriter writer, object value, Type type)
        {
            Type nullable = Nullable.GetUnderlyingType(type);
            if (nullable != null)
            {
                writer.Write(value != null);
                if (value != null) WriteValue(writer, value, nullable);
                return;
            }
            if (type.IsEnum) { WriteValue(writer, Convert.ChangeType(value, Enum.GetUnderlyingType(type)), Enum.GetUnderlyingType(type)); return; }
            if (type == typeof(bool)) writer.Write((bool)value);
            else if (type == typeof(byte)) writer.Write((byte)value);
            else if (type == typeof(sbyte)) writer.Write((sbyte)value);
            else if (type == typeof(short)) writer.Write((short)value);
            else if (type == typeof(ushort)) writer.Write((ushort)value);
            else if (type == typeof(int)) writer.Write((int)value);
            else if (type == typeof(uint)) writer.Write((uint)value);
            else if (type == typeof(long)) writer.Write((long)value);
            else if (type == typeof(ulong)) writer.Write((ulong)value);
            else if (type == typeof(float)) writer.Write((float)value);
            else if (type == typeof(double)) writer.Write((double)value);
            else if (type == typeof(char)) writer.Write((char)value);
            else if (type == typeof(string))
            {
                writer.Write(value != null);
                if (value != null) writer.Write((string)value);
            }
            else if (type.IsArray) WriteArray(writer, (Array)value, type.GetElementType());
            else WriteObject(writer, value, type);
        }

        private static object ReadValue(BinaryReader reader, Type type, object current)
        {
            Type nullable = Nullable.GetUnderlyingType(type);
            if (nullable != null)
            {
                if (!reader.ReadBoolean()) return null;
                return ReadValue(reader, nullable, null);
            }
            if (type.IsEnum) return Enum.ToObject(type, ReadValue(reader, Enum.GetUnderlyingType(type), null));
            if (type == typeof(bool)) return reader.ReadBoolean();
            if (type == typeof(byte)) return reader.ReadByte();
            if (type == typeof(sbyte)) return reader.ReadSByte();
            if (type == typeof(short)) return reader.ReadInt16();
            if (type == typeof(ushort)) return reader.ReadUInt16();
            if (type == typeof(int)) return reader.ReadInt32();
            if (type == typeof(uint)) return reader.ReadUInt32();
            if (type == typeof(long)) return reader.ReadInt64();
            if (type == typeof(ulong)) return reader.ReadUInt64();
            if (type == typeof(float)) return reader.ReadSingle();
            if (type == typeof(double)) return reader.ReadDouble();
            if (type == typeof(char)) return reader.ReadChar();
            if (type == typeof(string)) return reader.ReadBoolean() ? reader.ReadString() : null;
            if (type.IsArray) return ReadArray(reader, type.GetElementType(), (Array)current);
            if (!type.IsValueType && !reader.ReadBoolean()) return null;
            object target = current ?? Activator.CreateInstance(type, true);
            ReadObjectBody(reader, target, type);
            return target;
        }

        private static void ReadObjectBody(BinaryReader reader, object target, Type type)
        {
            var fields = new Dictionary<string, FieldInfo>();
            foreach (FieldInfo field in Fields(type)) fields[FieldKey(field)] = field;
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string name = reader.ReadString();
                if (!fields.TryGetValue(name, out FieldInfo field))
                    throw new InvalidDataException($"Save-state field '{type.Name}.{name}' is not available.");
                object current = field.GetValue(target);
                object restored = ReadValue(reader, field.FieldType, current);
                if (!field.IsInitOnly &&
                    (field.FieldType.IsValueType || !ReferenceEquals(restored, current)))
                    field.SetValue(target, restored);
            }
        }

        private static void WriteArray(BinaryWriter writer, Array array, Type elementType)
        {
            writer.Write(array != null);
            if (array == null) return;
            writer.Write(array.Length);
            for (int i = 0; i < array.Length; i++) WriteValue(writer, array.GetValue(i), elementType);
        }

        private static Array ReadArray(BinaryReader reader, Type elementType, Array current)
        {
            if (!reader.ReadBoolean()) return null;
            int length = reader.ReadInt32();
            Array result = current != null && current.Length == length
                ? current : Array.CreateInstance(elementType, length);
            for (int i = 0; i < length; i++)
                result.SetValue(ReadValue(reader, elementType, result.GetValue(i)), i);
            return result;
        }

        private static List<FieldInfo> Fields(Type type)
        {
            var result = new List<FieldInfo>();
            for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                foreach (FieldInfo field in current.GetFields(BindingFlags.Instance |
                             BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.IsStatic || field.IsLiteral || IsConnection(field.FieldType) ||
                        field.Name == "prgRom" || field.Name == "chrRom" ||
                        field.DeclaringType == typeof(Apu2A03) &&
                        (field.Name == "samples" || field.Name == "readIndex" ||
                         field.Name == "writeIndex")) continue;
                    result.Add(field);
                }
            }
            result.Sort((a, b) => string.CompareOrdinal(
                a.DeclaringType.FullName + "." + a.Name, b.DeclaringType.FullName + "." + b.Name));
            return result;
        }

        private static bool IsConnection(Type type) =>
            typeof(Delegate).IsAssignableFrom(type) || typeof(IMapper).IsAssignableFrom(type) ||
            type == typeof(Cpu6502) || type == typeof(Ppu2C02) || type == typeof(Apu2A03) ||
            type == typeof(CpuBus) || type == typeof(NesController) ||
            // These objects are presentation caches rebuilt by PPU rendering.
            // Saving them adds over a megabyte and makes exact restore depend on
            // transient pixels that are not part of the emulated machine state.
            type == typeof(PpuFrameBuffer) || type == typeof(PpuSceneSnapshot);

        private static string FieldKey(FieldInfo field) =>
            field.DeclaringType.FullName + "." + field.Name;
    }
}
