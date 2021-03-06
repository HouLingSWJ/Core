﻿namespace ConfigManagerEditor
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using System.Reflection;

    public class Serializer 
    {
        public static object Serialize(List<ConfigSource> sources)
        {
            Type t = FindType("SerializableSet");
            if (t == null)
            {
                Debug.LogError("找不到SerializableSet类！");
                return null;
            }

            object set = ScriptableObject.CreateInstance(t);
            foreach (ConfigSource source in sources)
            {
                string fieldName = source.sourceName + "s";
                Array configs = Source2Configs(source);
                FieldInfo fieldInfo = t.GetField(fieldName);
                Debug.Log(fieldInfo);
                Debug.Log(configs);
                fieldInfo.SetValue(set, configs);
            }
            return set;
        }

        /// <summary>
        /// 从源数据反射为对应的配置数组
        /// </summary>
        /// <returns></returns>
        private static Array Source2Configs(ConfigSource source)
        {
            Type configType = FindType(source.configName);

            int count = source.row - 3;
            Array configs = Array.CreateInstance(configType, count);
            for (int y = 3, i = 0; i < count; y++, i++)
            {
                object config = Activator.CreateInstance(configType);
                for (int x = 0; x < source.column; x++)
                {
                    string valueType = source.matrix[1, x];
                    string valueField = source.matrix[2, x];
                    string valueString = source.matrix[y, x];
                    FieldInfo field = configType.GetField(valueField);

                    try
                    {
                        object value = ConfigTools.SourceValue2Object(valueType, valueString);
                        field.SetValue(config, value);
                    }
                    catch
                    {
                        UnityEngine.Debug.LogError(string.Format("SourceValue2Object Error!valueType={0},valueString={1}", valueType, valueString));
                    }
                }
                configs.SetValue(config, i);
            }
            return configs;
        }

        private static Type FindType(string qualifiedTypeName)
        {
            Type t = Type.GetType(qualifiedTypeName);

            if (t != null)
            {
                return t;
            }
            else
            {
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = asm.GetType(qualifiedTypeName);
                    if (t != null)
                    {
                        return t;
                    }
                }
                return null;
            }
        }
    }

    

    
}

