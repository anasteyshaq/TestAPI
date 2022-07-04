using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace TestAPI
{
    public class JsonSerializer
    {
        public static T DeserializeObject<T>(string text)
        {
            return new JsonSerializer().Deserialize<T>(text);
        }

        public T Deserialize<T>(string text)
        {
            return Deserialize<T>(text, new JsonParser());
        }

        public T Deserialize<T>(string text, JsonParser parser)
        {
            if (parser == null)
            {
                throw new ArgumentException("An invalid argument was specified.", "parser");
            }

            var o = parser.Parse(text);
            return (T)Deserialize(o, typeof(T));
        }

        public JsonSerializer()
        {
            TypeInfoPropertyName = "@type";
        }

        public bool UseTypeInfo { get; set; }

        public string TypeInfoPropertyName { get; set; }

        private Dictionary<string, Type> TypeCache = new Dictionary<string, Type>();

        private object Deserialize(object from, Type type)
        {
            if (from == null)
                return null;

            var dict = from as IDictionary<string, object>;
            if (dict != null)
            {
                if (UseTypeInfo)
                {
                    object typeNameObject;

                    if (dict.TryGetValue(TypeInfoPropertyName, out typeNameObject))
                    {
                        var typeName = typeNameObject as string;

                        if (!string.IsNullOrEmpty(typeName))
                        {
                            Type derivedType;

                            if (!TypeCache.TryGetValue(typeName, out derivedType))
                            {
                                derivedType = type.Assembly.GetTypes()
                                    .FirstOrDefault(t => t != type && type.IsAssignableFrom(t) && string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase));
                                TypeCache[typeName] = derivedType ?? typeof(object);
                            }

                            if (derivedType != null && derivedType != typeof(object)) type = derivedType;
                        }
                    }
                }

                var to = Activator.CreateInstance(type);
                DeserializeDictionary(dict, to);
                return to;
            }

            var list = from as IList;
            if (list != null)
            {
                var to = (IList)Activator.CreateInstance(type);
                DeserializeList(list, to);
            }

            if (typeof(IList).IsAssignableFrom(type))
            {
                var to = (IList)Activator.CreateInstance(type);
                var itemType = to.GetType().GetProperty("Item").PropertyType;
                to.Add(Deserialize(from, itemType));
                return to;
            }

            if (type.IsEnum)
            {
                return Enum.Parse(type, from.ToString(), true);
            }

            if (!type.IsAssignableFrom(from.GetType()))
            {
                // Nullable handling
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    type = type.GetGenericArguments()[0];
                }

                return Convert.ChangeType(from, type, CultureInfo.InvariantCulture);
            }

            return from;
        }

        private void DeserializeList(IList from, IList to)
        {
            var itemType = to.GetType().GetProperty("Item").PropertyType;
            foreach (var item in from)
            {
                to.Add(Deserialize(item, itemType));
            }
        }

        private void DeserializeDictionary(IEnumerable<KeyValuePair<string, object>> from, object to)
        {
            var type = to.GetType();

            var dict = to as IDictionary;
            if (dict != null)
            {
                var valType = typeof(object);
                while (type != typeof(object))
                {
                    if (type.IsGenericType)
                    {
                        valType = type.GetGenericArguments()[1];
                        break;
                    }

                    type = type.BaseType;
                }

                foreach (var pair in from)
                {
                    dict[pair.Key] = Deserialize(pair.Value, valType);
                }
            }
            else
            {
                foreach (var pair in from)
                {
                    var member = GetMember(type, pair.Key);
                    if (member != null)
                    {
                        member.Set(to, Deserialize(pair.Value, member.Type));
                    }
                }
            }
        }

        class SetterMember
        {
            public Type Type { get; set; }
            public Action<object, object> Set { get; set; }
        }

        private Dictionary<string, SetterMember> MemberCache = new Dictionary<string, SetterMember>();

        private SetterMember GetMember(Type type, string name)
        {
            SetterMember member;
            var key = name + type.GetHashCode();
            if (!MemberCache.TryGetValue(key, out member))
            {
                var fieldInfo = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (fieldInfo != null)
                {
                    member = new SetterMember
                    {
                        Type = fieldInfo.FieldType,
                        Set = (o, v) => fieldInfo.SetValue(o, v)
                    };

                    MemberCache[key] = member;
                }
                else
                {
                    var propertyInfo = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                    if (propertyInfo != null && propertyInfo.CanWrite)
                    {
                        member = new SetterMember
                        {
                            Type = propertyInfo.PropertyType,
                            Set = (o, v) => propertyInfo.SetValue(o, v, null)
                        };

                        MemberCache[key] = member;
                    }
                    else
                    {
                        MemberCache[key] = null;

                    }
                }
            }

            return member;
        }
        public string Serialize(object obj)
        {
            if (obj == null) return "null";

            var list = obj as IList;
            if (list != null && !(obj is IEnumerable<KeyValuePair<string, object>>))
            {
                var sb = new StringBuilder("[");
                if (list.Count > 0)
                {
                    sb.Append(string.Join(",", list.Cast<object>().Select(i => Serialize(i)).ToArray()));
                }
                sb.Append("]");
                return sb.ToString();
            }

            var str = obj as string;
            if (str != null)
            {
                return @"""" + EscapeString(str) + @"""";
            }

            if (obj is int)
            {
                return obj.ToString();
            }

            var b = obj as bool?;
            if (b.HasValue)
            {
                return b.Value ? "true" : "false";
            }

            if (obj is decimal)
            {
                return ((IFormattable)obj).ToString("G", NumberFormatInfo.InvariantInfo);
            }

            if (obj is double || obj is float)
            {
                return ((IFormattable)obj).ToString("R", NumberFormatInfo.InvariantInfo);
            }

            if (obj is Enum)
            {
                return @"""" + EscapeString(obj.ToString()) + @"""";
            }

            if (obj is char)
            {
                return @"""" + obj + @"""";
            }

            if (obj.GetType().IsPrimitive)
            {
                return (string)Convert.ChangeType(obj, typeof(string), CultureInfo.InvariantCulture);
            }
            return SerializeComplexType(obj);
        }

        private static string EscapeString(string src)
        {
            var sb = new StringBuilder();

            foreach (var c in src)
            {
                if (c == '"' || c == '\\')
                {
                    sb.Append('\\');
                    sb.Append(c);
                }
                else if ((int)c < 0x20) // control character
                {
                    var u = (int)c;
                    switch (u)
                    {
                        case '\b':
                            sb.Append("\\b");
                            break;
                        case '\f':
                            sb.Append("\\f");
                            break;
                        case '\n':
                            sb.Append("\\n");
                            break;
                        case '\r':
                            sb.Append("\\r");
                            break;
                        case '\t':
                            sb.Append("\\t");
                            break;
                        default:
                            sb.Append("\\u");
                            sb.Append(u.ToString("X4", NumberFormatInfo.InvariantInfo));
                            break;
                    }
                }
                else
                    sb.Append(c);
            }

            return sb.ToString();
        }

        private string SerializeComplexType(object o)
        {
            var s = new StringBuilder("{");

            if (o is IDictionary || o is IEnumerable<KeyValuePair<string, object>>)
            {
                SerializeDictionary(o, s);
            }
            else
            {
                SerializeProperties(o, s);
            }

            s.Append("}");

            return s.ToString();
        }

        private void SerializeProperties(object o, StringBuilder s)
        {
            var type = o.GetType();
            var members = GetMembers(type);

            if (UseTypeInfo && ((type.BaseType != typeof(object) && type.BaseType != null) || type.GetInterfaces().Any()))
            {
                // emit type info
                s.Append(@"""");
                s.Append(TypeInfoPropertyName);
                s.Append(@""":""");
                s.Append(type.Name);
                s.Append(@""",");
            }

            foreach (var member in members)
            {
                object val = member.Get(o);

                if (val != null && (member.DefaultValue == null || !val.Equals(member.DefaultValue)))
                {
                    var v = Serialize(val);
                    s.Append(@"""");
                    s.Append(member.Name);
                    s.Append(@""":");
                    s.Append(v);
                    s.Append(",");
                }
            }

            if (s.Length > 0 && s[s.Length - 1] == ',') s.Remove(s.Length - 1, 1);
        }

        private void SerializeDictionary(object o, StringBuilder s)
        {
            IEnumerable<KeyValuePair<string, object>> kvps;
            var dict = o as IDictionary;
            if (dict != null)
                kvps = dict.Keys.Cast<object>().Select(k => new KeyValuePair<string, object>(k.ToString(), dict[k]));
            else
                kvps = (IEnumerable<KeyValuePair<string, object>>)o;

            // work around MonoTouch Full-AOT issue
            var kvpList = kvps.ToList();
            kvpList.Sort((e1, e2) => string.Compare(e1.Key, e2.Key, StringComparison.OrdinalIgnoreCase));

            foreach (var kvp in kvpList)
            {
                s.Append(@"""");
                s.Append(kvp.Key);
                s.Append(@""":");
                s.Append(Serialize(kvp.Value));
                s.Append(",");
            }

            if (s.Length > 0 && s[s.Length - 1] == ',')
                s.Remove(s.Length - 1, 1);
        }

        class GetterMember
        {
            public string Name { get; set; }
            public Func<object, object> Get { get; set; }
            public object DefaultValue { get; set; }
        }

        private Dictionary<Type, GetterMember[]> MembersCache = new Dictionary<Type, GetterMember[]>();

        private GetterMember[] GetMembers(Type type)
        {
            GetterMember[] members;

            if (!MembersCache.TryGetValue(type, out members))
            {
                var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
                    .Where(p => p.CanWrite)
                    .Select(p => BuildGetterMember(p));

                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
                    .Select(f => BuildGetterMember(f));

                members = props.Concat(fields).OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToArray();

                MembersCache[type] = members;
            }

            return members;
        }

        private static GetterMember BuildGetterMember(PropertyInfo p)
        {
            var defaultAttribute = p.GetCustomAttributes(typeof(DefaultValueAttribute), true).FirstOrDefault() as DefaultValueAttribute;
            return new GetterMember
            {
                Name = p.Name,
                Get = (Func<object, object>)(o => p.GetValue(o, null)),
                DefaultValue = defaultAttribute != null ? defaultAttribute.Value : GetDefaultValueForType(p.PropertyType)
            };
        }

        private static GetterMember BuildGetterMember(FieldInfo f)
        {
            var defaultAttribute = f.GetCustomAttributes(typeof(DefaultValueAttribute), true).FirstOrDefault() as DefaultValueAttribute;
            return new GetterMember
            {
                Name = f.Name,
                Get = (o => f.GetValue(o)),
                DefaultValue = defaultAttribute != null ? defaultAttribute.Value : GetDefaultValueForType(f.FieldType)
            };
        }

        private static object GetDefaultValueForType(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }
}
