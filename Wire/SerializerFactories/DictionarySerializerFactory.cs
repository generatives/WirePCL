﻿// //-----------------------------------------------------------------------
// // <copyright file="DictionarySerializerFactory.cs" company="Asynkron HB">
// //     Copyright (C) 2015-2016 Asynkron HB All rights reserved
// // </copyright>
// //-----------------------------------------------------------------------

using PCLReflectionExtensions;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Wire.Extensions;
using Wire.ValueSerializers;

namespace Wire.SerializerFactories
{
    public class DictionarySerializerFactory : ValueSerializerFactory
    {
        public override bool CanSerialize(Serializer serializer, Type type) => IsInterface(type);

        private static bool IsInterface(Type type)
        {
            return type
                .GetTypeInfo()
                .ImplementedInterfaces
                .Select(
                    t =>
                        t.IsGenericType() &&
                        t.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                .Any(isDict => isDict);
        }

        public override bool CanDeserialize(Serializer serializer, Type type) => IsInterface(type);

        public override ValueSerializer BuildSerializer(Serializer serializer, Type type,
            ConcurrentDictionary<Type, ValueSerializer> typeMapping)
        {
            var preserveObjectReferences = serializer.Options.PreserveObjectReferences;
            var ser = new ObjectSerializer(serializer.Options.FieldSelector, type);
            typeMapping.TryAdd(type, ser);
            var elementSerializer = serializer.GetSerializerByType(typeof(DictionaryEntry));

            ObjectReader reader = (stream, session) =>
            {
                throw new NotSupportedException("Generic IDictionary<TKey,TValue> are not yet supported");
#pragma warning disable CS0162 // Unreachable code detected
                var instance = Activator.CreateInstance(type);
#pragma warning restore CS0162 // Unreachable code detected
                if (preserveObjectReferences)
                {
                    session.TrackDeserializedObject(instance);
                }
                var count = stream.ReadInt32(session);
                var entries = new DictionaryEntry[count];
                for (var i = 0; i < count; i++)
                {
                    var entry = (DictionaryEntry) stream.ReadObject(session);
                    entries[i] = entry;
                }
                //TODO: populate dictionary
                return instance;
            };

            ObjectWriter writer = (stream, obj, session) =>
            {
                if (preserveObjectReferences)
                {
                    session.TrackSerializedObject(obj);
                }
                var dict = obj as IDictionary;
                // ReSharper disable once PossibleNullReferenceException
                Int32Serializer.WriteValueImpl(stream, dict.Count, session);
                foreach (var item in dict)
                {
                    stream.WriteObject(item, typeof(DictionaryEntry), elementSerializer,
                        serializer.Options.PreserveObjectReferences, session);
                    // elementSerializer.WriteValue(stream,item,session);
                }
            };
            ser.Initialize(reader, writer);

            return ser;
        }
    }
}