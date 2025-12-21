using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace XiaoZhi.Unity.IoT
{
    public static class Utils
    {
        public static string TypeString<T>()
        {
            var typeName = typeof(T).Name;
            return typeName switch
            {
                nameof(Byte) => "uint8",
                nameof(SByte) => "int8",
                nameof(Single) => "float",
                _ => typeName.ToLower()
            };
        }

        public static bool MaybeJson(string str)
        {
            return (str.StartsWith("{") && str.EndsWith("}")) ||
                   (str.StartsWith("[") && str.EndsWith("]"));
        }
    }

    public interface IProperty<out T> : IProperty
    {
        T GetValue();
    }

    public interface IProperty
    {
        string Name { get; }
        string Description { get; }
        Type ValueType { get; }
        void GetDescriptorJson(JsonWriter writer);
        void GetStateJson(JsonWriter writer);
    }

    public interface IParameter<T> : IParameter
    {
        T Value { get; set; }
    }

    public interface IParameter
    {
        bool Required { get; }
        string Name { get; }
        string Description { get; }
        Type ValueType { get; }
        string ValueString { get; }
        void GetDescriptorJson(JsonWriter writer);
    }

    public class Parameter<T> : IParameter<T>
    {
        public Parameter(string name, string description, bool required = true)
        {
            Name = name;
            Description = description;
            Required = required;
        }

        public string Name { get; }

        public string Description { get; }

        public bool Required { get; }

        public Type ValueType => typeof(T);

        public string ValueString => Value?.ToString() ?? "";

        public T Value { get; set; }

        public void GetDescriptorJson(JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("description");
            if (Utils.MaybeJson(Description)) writer.WriteRawValue(Description);
            else writer.WriteValue(Description);
            writer.WritePropertyName("type");
            writer.WriteValue(Utils.TypeString<T>());
            writer.WriteEndObject();
        }
    }

    public class ParameterList : IEnumerable<IParameter>
    {
        private readonly Dictionary<string, IParameter> _parameters = new();

        public ParameterList()
        {
        }

        public ParameterList(IEnumerable<IParameter> parameters)
        {
            foreach (var parameter in parameters)
                _parameters[parameter.Name] = parameter;
        }

        public void AddParameter<T>(string name, string description, bool required = true)
        {
            _parameters[name] = new Parameter<T>(name, description, required);
        }

        public void AddParameter(IParameter parameter)
        {
            _parameters[parameter.Name] = parameter;
        }

        public IParameter this[string name] => _parameters.GetValueOrDefault(name);

        public T GetValue<T>(string name)
        {
            if (this[name] is not IParameter<T> parameter)
            {
                Debug.LogError($"Cannot cast parameter value to type {typeof(T).Name}");
                return default;
            }

            return parameter.Value;
        }

        public void SetValue<T>(string name, T value)
        {
            if (this[name] is not IParameter<T> parameter)
            {
                Debug.LogError($"Cannot cast parameter value to type {typeof(T).Name}");
                return;
            }

            parameter.Value = value;
        }

        public void GetDescriptorJson(JsonWriter writer)
        {
            writer.WriteStartObject();
            foreach (var parameter in _parameters.Values)
            {
                writer.WritePropertyName(parameter.Name);
                parameter.GetDescriptorJson(writer);
            }

            writer.WriteEndObject();
        }

        public IEnumerator<IParameter> GetEnumerator()
        {
            return _parameters.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _parameters.Values.GetEnumerator();
        }
    }

    public class Property<T> : IProperty<T>
    {
        private readonly Func<T> _getter;

        public Property(string name, string description, Func<T> getter)
        {
            Name = name;
            Description = description;
            _getter = getter;
        }

        public string Name { get; }

        public string Description { get; }

        public T Value => _getter();

        public Type ValueType => typeof(T);

        public T GetValue() => Value;

        public void GetDescriptorJson(JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("description");
            if (Utils.MaybeJson(Description)) writer.WriteRawValue(Description);
            else writer.WriteValue(Description);
            writer.WritePropertyName("type");
            writer.WriteValue(Utils.TypeString<T>());
            writer.WriteEndObject();
        }

        public void GetStateJson(JsonWriter writer)
        {
            writer.WriteValue(Value);
        }
    }

    public class PropertyList
    {
        private readonly Dictionary<string, IProperty> _properties = new();

        public PropertyList()
        {
        }

        public PropertyList(IEnumerable<IProperty> properties)
        {
            foreach (var property in properties)
                _properties[property.Name] = property;
        }

        public void Clear()
        {
            _properties.Clear();
        }

        public void AddProperty<T>(string name, string description, Func<T> getter)
        {
            _properties[name] = new Property<T>(name, description, getter);
        }

        public void AddProperty(IProperty property)
        {
            _properties[property.Name] = property;
        }

        public void RemoveProperty(string name)
        {
            _properties.Remove(name);
        }

        public IProperty this[string name] => _properties.GetValueOrDefault(name);

        public T GetValue<T>(string name)
        {
            if (this[name] is not IProperty<T> property)
            {
                Debug.LogError($"Cannot cast property value to type {typeof(T).Name}");
                return default;
            }

            return property.GetValue();
        }

        public void GetDescriptorJson(JsonWriter writer)
        {
            writer.WriteStartObject();
            foreach (var property in _properties.Values)
            {
                writer.WritePropertyName(property.Name);
                property.GetDescriptorJson(writer);
            }

            writer.WriteEndObject();
        }

        public void GetStateJson(JsonWriter writer)
        {
            writer.WriteStartObject();
            foreach (var property in _properties.Values)
            {
                writer.WritePropertyName(property.Name);
                property.GetStateJson(writer);
            }

            writer.WriteEndObject();
        }
    }

    public class Method
    {
        private readonly string _name;
        private readonly string _description;
        private readonly ParameterList _parameters;
        private readonly Action<ParameterList> _callback;

        public Method(string name, string description, ParameterList parameters, Action<ParameterList> callback)
        {
            _name = name;
            _description = description;
            _parameters = parameters;
            _callback = callback;
        }

        public string Name => _name;
        public string Description => _description;
        public ParameterList Parameters => _parameters;

        public void GetDescriptorJson(JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("description");
            if (Utils.MaybeJson(Description)) writer.WriteRawValue(Description);
            else writer.WriteValue(Description);
            writer.WritePropertyName("parameters");
            _parameters.GetDescriptorJson(writer);
            writer.WriteEndObject();
        }

        public void Invoke()
        {
            _callback(_parameters);
        }
    }

    public class MethodList
    {
        private readonly Dictionary<string, Method> _methods = new();

        public MethodList()
        {
        }

        public MethodList(IEnumerable<Method> methods)
        {
            foreach (var method in methods) _methods[method.Name] = method;
        }

        public void Clear()
        {
            _methods.Clear();
        }

        public void AddMethod(string name, string description, ParameterList parameters, Action<ParameterList> callback)
        {
            _methods[name] = new Method(name, description, parameters, callback);
        }

        public void RemoveMethod(string name)
        {
            _methods.Remove(name);
        }

        public Method this[string name] => _methods.GetValueOrDefault(name);

        public void GetDescriptorJson(JsonWriter writer)
        {
            writer.WriteStartObject();
            foreach (var method in _methods.Values)
            {
                writer.WritePropertyName(method.Name);
                method.GetDescriptorJson(writer);
            }

            writer.WriteEndObject();
        }
    }

    public abstract class Thing : IDisposable
    {
        protected readonly PropertyList _properties;
        protected readonly MethodList _methods;
        protected Context _context;

        public string Name { get; }

        public string Description { get; }

        protected Thing(string name, string description)
        {
            Name = name;
            Description = description;
            _properties = new PropertyList();
            _methods = new MethodList();
        }

        public void Inject(Context ctx)
        {
            _context = ctx;
        }

        public virtual async UniTask Load()
        {
            await UniTask.CompletedTask;
        }

        public void GetDescriptorJson(JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("name");
            writer.WriteValue(Name);
            writer.WritePropertyName("description");
            writer.WriteValue(Description);
            writer.WritePropertyName("properties");
            _properties.GetDescriptorJson(writer);
            writer.WritePropertyName("methods");
            _methods.GetDescriptorJson(writer);
            writer.WriteEndObject();
        }

        public string GetStateJson()
        {
            using var stringWriter = new StringWriter();
            using var jsonWriter = new JsonTextWriter(stringWriter);
            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName("name");
            jsonWriter.WriteValue(Name);
            jsonWriter.WritePropertyName("state");
            _properties.GetStateJson(jsonWriter);
            jsonWriter.WriteEndObject();
            return stringWriter.ToString();
        }

        public void Invoke(JToken command)
        {
            try
            {
                var methodName = command["method"].Value<string>();
                var inputParams = command["parameters"];
                var method = _methods[methodName];
                if (method == null)
                {
                    Debug.LogError($"Method not found: {methodName}");
                    return;
                }

                foreach (var param in method.Parameters)
                {
                    var inputParam = inputParams?[param.Name];
                    if (param.Required && inputParam == null)
                        throw new ArgumentException($"Parameter {param.Name} is required");
                    if (inputParam == null) continue;
                    var genericType = param.ValueType;
                    var commandValue = typeof(Extensions).GetMethod("Value", BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(IEnumerable<JToken>) }, null)!.MakeGenericMethod(genericType);
                    var value = commandValue.Invoke(null, new object[] { inputParam });
                    var paramValue = param.GetType().GetProperty("Value");
                    paramValue!.SetValue(param, value);
                }

                method.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Method invocation failed: {ex}");
            }
        }

        public virtual void Dispose()
        {
        }
    }
}