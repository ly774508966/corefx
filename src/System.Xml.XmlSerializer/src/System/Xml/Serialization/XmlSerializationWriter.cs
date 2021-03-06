// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Xml.Serialization
{
    using System;
    using System.IO;
    using System.Collections;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Xml.Schema;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.CodeDom.Compiler;
    using System.Globalization;
    using System.Text;
    using System.Threading;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Xml.Extensions;
    using XmlSchema = System.ServiceModel.Dispatcher.XmlSchemaConstants;

    ///<internalonly/>
    public abstract class XmlSerializationWriter : XmlSerializationGeneratedCode
    {
        private XmlWriter _w;
        private XmlSerializerNamespaces _namespaces;
        private int _tempNamespacePrefix;
        private HashSet<int> _usedPrefixes;
        private HashSet<object> _objectsInUse;
        private string _aliasBase = "q";
        private bool _escapeName = true;

        // this method must be called before any generated serialization methods are called
        internal void Init(XmlWriter w, XmlSerializerNamespaces namespaces, string encodingStyle, string idBase)
        {
            _w = w;
            _namespaces = namespaces;
        }

        // this method must be called before any generated serialization methods are called
        internal void Init(XmlWriter w, XmlSerializerNamespaces namespaces, string encodingStyle, string idBase, TempAssembly tempAssembly)
        {
            Init(w, namespaces, encodingStyle, idBase);
            Init(tempAssembly);
        }

        protected bool EscapeName
        {
            get
            {
                return _escapeName;
            }
            set
            {
                _escapeName = value;
            }
        }

        protected XmlWriter Writer
        {
            get
            {
                return _w;
            }
            set
            {
                _w = value;
            }
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        protected IList XmlNamespaces
        {
            get
            {
                return _namespaces == null ? null : _namespaces.NamespaceList;
            }
            set
            {
                if (value == null)
                {
                    _namespaces = null;
                }
                else
                {
                    Array array = Array.CreateInstance(typeof(XmlQualifiedName), value.Count);
                    value.CopyTo(array, 0);
                    XmlQualifiedName[] qnames = (XmlQualifiedName[])array;

                    _namespaces = new XmlSerializerNamespaces(qnames);
                }
            }
        }

        protected static byte[] FromByteArrayBase64(byte[] value)
        {
            // Unlike other "From" functions that one is just a place holder for automatic code generation.
            // The reason is performance and memory consumption for (potentially) big 64base-encoded chunks
            // And it is assumed that the caller generates the code that will distinguish between byte[] and string return types
            //
            return value;
        }


        protected static string FromByteArrayHex(byte[] value)
        {
            return XmlCustomFormatter.FromByteArrayHex(value);
        }

        protected static string FromDateTime(DateTime value)
        {
            return XmlCustomFormatter.FromDateTime(value);
        }

        protected static string FromDate(DateTime value)
        {
            return XmlCustomFormatter.FromDate(value);
        }

        protected static string FromTime(DateTime value)
        {
            return XmlCustomFormatter.FromTime(value);
        }

        protected static string FromChar(char value)
        {
            return XmlCustomFormatter.FromChar(value);
        }

        protected static string FromEnum(long value, string[] values, long[] ids)
        {
            return XmlCustomFormatter.FromEnum(value, values, ids, null);
        }

        protected static string FromEnum(long value, string[] values, long[] ids, string typeName)
        {
            return XmlCustomFormatter.FromEnum(value, values, ids, typeName);
        }

        protected static string FromXmlName(string name)
        {
            return XmlCustomFormatter.FromXmlName(name);
        }

        protected static string FromXmlNCName(string ncName)
        {
            return XmlCustomFormatter.FromXmlNCName(ncName);
        }

        protected static string FromXmlNmToken(string nmToken)
        {
            return XmlCustomFormatter.FromXmlNmToken(nmToken);
        }

        protected static string FromXmlNmTokens(string nmTokens)
        {
            return XmlCustomFormatter.FromXmlNmTokens(nmTokens);
        }

        protected void WriteXsiType(string name, string ns)
        {
            WriteAttribute("type", XmlSchema.InstanceNamespace, GetQualifiedName(name, ns));
        }

        private XmlQualifiedName GetPrimitiveTypeName(Type type)
        {
            return GetPrimitiveTypeName(type, true);
        }

        private XmlQualifiedName GetPrimitiveTypeName(Type type, bool throwIfUnknown)
        {
            XmlQualifiedName qname = GetPrimitiveTypeNameInternal(type);
            if (throwIfUnknown && qname == null)
                throw CreateUnknownTypeException(type);
            return qname;
        }

        internal static XmlQualifiedName GetPrimitiveTypeNameInternal(Type type)
        {
            string typeName;
            string typeNs = XmlSchema.Namespace;

            switch (type.GetTypeCode())
            {
                case TypeCode.String: typeName = "string"; break;
                case TypeCode.Int32: typeName = "int"; break;
                case TypeCode.Boolean: typeName = "boolean"; break;
                case TypeCode.Int16: typeName = "short"; break;
                case TypeCode.Int64: typeName = "long"; break;
                case TypeCode.Single: typeName = "float"; break;
                case TypeCode.Double: typeName = "double"; break;
                case TypeCode.Decimal: typeName = "decimal"; break;
                case TypeCode.DateTime: typeName = "dateTime"; break;
                case TypeCode.Byte: typeName = "unsignedByte"; break;
                case TypeCode.SByte: typeName = "byte"; break;
                case TypeCode.UInt16: typeName = "unsignedShort"; break;
                case TypeCode.UInt32: typeName = "unsignedInt"; break;
                case TypeCode.UInt64: typeName = "unsignedLong"; break;
                case TypeCode.Char:
                    typeName = "char";
                    typeNs = UrtTypes.Namespace;
                    break;
                default:
                    if (type == typeof(XmlQualifiedName)) typeName = "QName";
                    else if (type == typeof(byte[])) typeName = "base64Binary";
                    else if (type == typeof(Guid))
                    {
                        typeName = "guid";
                        typeNs = UrtTypes.Namespace;
                    }
                    else if (type == typeof (TimeSpan))
                    {
                        typeName = "TimeSpan";
                        typeNs = UrtTypes.Namespace;
                    }
                    else if (type == typeof (XmlNode[]))
                    {
                        typeName = Soap.UrType;
                    }
                    else
                        return null;
                    break;
            }
            return new XmlQualifiedName(typeName, typeNs);
        }

        protected void WriteTypedPrimitive(string name, string ns, object o, bool xsiType)
        {
            string value = null;
            string type;
            string typeNs = XmlSchema.Namespace;
            bool writeRaw = true;
            bool writeDirect = false;
            Type t = o.GetType();
            bool wroteStartElement = false;

            switch (t.GetTypeCode())
            {
                case TypeCode.String:
                    value = (string)o;
                    type = "string";
                    writeRaw = false;
                    break;
                case TypeCode.Int32:
                    value = XmlConvert.ToString((int)o);
                    type = "int";
                    break;
                case TypeCode.Boolean:
                    value = XmlConvert.ToString((bool)o);
                    type = "boolean";
                    break;
                case TypeCode.Int16:
                    value = XmlConvert.ToString((short)o);
                    type = "short";
                    break;
                case TypeCode.Int64:
                    value = XmlConvert.ToString((long)o);
                    type = "long";
                    break;
                case TypeCode.Single:
                    value = XmlConvert.ToString((float)o);
                    type = "float";
                    break;
                case TypeCode.Double:
                    value = XmlConvert.ToString((double)o);
                    type = "double";
                    break;
                case TypeCode.Decimal:
                    value = XmlConvert.ToString((decimal)o);
                    type = "decimal";
                    break;
                case TypeCode.DateTime:
                    value = FromDateTime((DateTime)o);
                    type = "dateTime";
                    break;
                case TypeCode.Char:
                    value = FromChar((char)o);
                    type = "char";
                    typeNs = UrtTypes.Namespace;
                    break;
                case TypeCode.Byte:
                    value = XmlConvert.ToString((byte)o);
                    type = "unsignedByte";
                    break;
                case TypeCode.SByte:
                    value = XmlConvert.ToString((sbyte)o);
                    type = "byte";
                    break;
                case TypeCode.UInt16:
                    value = XmlConvert.ToString((UInt16)o);
                    type = "unsignedShort";
                    break;
                case TypeCode.UInt32:
                    value = XmlConvert.ToString((UInt32)o);
                    type = "unsignedInt";
                    break;
                case TypeCode.UInt64:
                    value = XmlConvert.ToString((UInt64)o);
                    type = "unsignedLong";
                    break;

                default:
                    if (t == typeof(XmlQualifiedName))
                    {
                        type = "QName";
                        // need to write start element ahead of time to establish context
                        // for ns definitions by FromXmlQualifiedName
                        wroteStartElement = true;
                        if (name == null)
                            _w.WriteStartElement(type, typeNs);
                        else
                            _w.WriteStartElement(name, ns);
                        value = FromXmlQualifiedName((XmlQualifiedName)o, false);
                    }
                    else if (t == typeof(byte[]))
                    {
                        value = String.Empty;
                        writeDirect = true;
                        type = "base64Binary";
                    }
                    else if (t == typeof(Guid))
                    {
                        value = XmlConvert.ToString((Guid)o);
                        type = "guid";
                        typeNs = UrtTypes.Namespace;
                    }
                    else if (t == typeof(TimeSpan))
                    {
                        value = XmlConvert.ToString((TimeSpan)o);
                        type = "TimeSpan";
                        typeNs = UrtTypes.Namespace;
                    }
                    else if (typeof(XmlNode[]).IsAssignableFrom(t))
                    {
                        if (name == null)
                            _w.WriteStartElement(Soap.UrType, XmlSchema.Namespace);
                        else
                            _w.WriteStartElement(name, ns);

                        XmlNode[] xmlNodes = (XmlNode[])o;
                        for (int i = 0; i < xmlNodes.Length; i++)
                        {
                            if (xmlNodes[i] == null)
                                continue;
                            xmlNodes[i].WriteTo(_w);
                        }
                        _w.WriteEndElement();
                        return;
                    }
                    else
                        throw CreateUnknownTypeException(t);
                    break;
            }
            if (!wroteStartElement)
            {
                if (name == null)
                    _w.WriteStartElement(type, typeNs);
                else
                    _w.WriteStartElement(name, ns);
            }

            if (xsiType) WriteXsiType(type, typeNs);

            if (value == null)
            {
                _w.WriteAttributeString("nil", XmlSchema.InstanceNamespace, "true");
            }
            else if (writeDirect)
            {
                // only one type currently writes directly to XML stream
                XmlCustomFormatter.WriteArrayBase64(_w, (byte[])o, 0, ((byte[])o).Length);
            }
            else if (writeRaw)
            {
                _w.WriteRaw(value);
            }
            else
                _w.WriteString(value);
            _w.WriteEndElement();
        }

        private string GetQualifiedName(string name, string ns)
        {
            if (ns == null || ns.Length == 0) return name;
            string prefix = _w.LookupPrefix(ns);
            if (prefix == null)
            {
                if (ns == XmlReservedNs.NsXml)
                {
                    prefix = "xml";
                }
                else
                {
                    prefix = NextPrefix();
                    WriteAttribute("xmlns", prefix, null, ns);
                }
            }
            else if (prefix.Length == 0)
            {
                return name;
            }
            return prefix + ":" + name;
        }

        protected string FromXmlQualifiedName(XmlQualifiedName xmlQualifiedName)
        {
            return FromXmlQualifiedName(xmlQualifiedName, true);
        }

        protected string FromXmlQualifiedName(XmlQualifiedName xmlQualifiedName, bool ignoreEmpty)
        {
            if (xmlQualifiedName == null) return null;
            if (xmlQualifiedName.IsEmpty && ignoreEmpty) return null;
            return GetQualifiedName(EscapeName ? XmlConvert.EncodeLocalName(xmlQualifiedName.Name) : xmlQualifiedName.Name, xmlQualifiedName.Namespace);
        }

        protected void WriteStartElement(string name)
        {
            WriteStartElement(name, null, null, false, null);
        }

        protected void WriteStartElement(string name, string ns)
        {
            WriteStartElement(name, ns, null, false, null);
        }

        protected void WriteStartElement(string name, string ns, bool writePrefixed)
        {
            WriteStartElement(name, ns, null, writePrefixed, null);
        }

        protected void WriteStartElement(string name, string ns, object o)
        {
            WriteStartElement(name, ns, o, false, null);
        }

        protected void WriteStartElement(string name, string ns, object o, bool writePrefixed)
        {
            WriteStartElement(name, ns, o, writePrefixed, null);
        }

        protected void WriteStartElement(string name, string ns, object o, bool writePrefixed, XmlSerializerNamespaces xmlns)
        {
            if (o != null && _objectsInUse != null)
            {
                if (!_objectsInUse.Add(o))
                    throw new InvalidOperationException(SR.Format(SR.XmlCircularReference, o.GetType().FullName));
            }

            string prefix = null;
            bool needEmptyDefaultNamespace = false;
            if (_namespaces != null)
            {
                foreach (string alias in _namespaces.Namespaces.Keys)
                {
                    string aliasNs = (string)_namespaces.Namespaces[alias];

                    if (alias.Length > 0 && aliasNs == ns)
                        prefix = alias;
                    if (alias.Length == 0)
                    {
                        if (aliasNs == null || aliasNs.Length == 0)
                            needEmptyDefaultNamespace = true;
                        if (ns != aliasNs)
                            writePrefixed = true;
                    }
                }
                _usedPrefixes = ListUsedPrefixes(_namespaces.Namespaces, _aliasBase);
            }
            if (writePrefixed && prefix == null && ns != null && ns.Length > 0)
            {
                prefix = _w.LookupPrefix(ns);
                if (prefix == null || prefix.Length == 0)
                {
                    prefix = NextPrefix();
                }
            }
            if (prefix == null && xmlns != null)
            {
                prefix = xmlns.LookupPrefix(ns);
            }
            if (needEmptyDefaultNamespace && prefix == null && ns != null && ns.Length != 0)
                prefix = NextPrefix();
            _w.WriteStartElement(prefix, name, ns);
            if (_namespaces != null)
            {
                foreach (string alias in _namespaces.Namespaces.Keys)
                {
                    string aliasNs = (string)_namespaces.Namespaces[alias];
                    if (alias.Length == 0 && (aliasNs == null || aliasNs.Length == 0))
                        continue;
                    if (aliasNs == null || aliasNs.Length == 0)
                    {
                        if (alias.Length > 0)
                            throw new InvalidOperationException(SR.Format(SR.XmlInvalidXmlns, alias));
                        WriteAttribute("xmlns", alias, null, aliasNs);
                    }
                    else
                    {
                        if (_w.LookupPrefix(aliasNs) == null)
                        {
                            // write the default namespace declaration only if we have not written it already, over wise we just ignore one provided by the user
                            if (prefix == null && alias.Length == 0)
                                break;
                            WriteAttribute("xmlns", alias, null, aliasNs);
                        }
                    }
                }
            }
            WriteNamespaceDeclarations(xmlns);
        }

        private HashSet<int> ListUsedPrefixes(Dictionary<string, string> nsList, string prefix)
        {
            var qnIndexes = new HashSet<int>();
            int prefixLength = prefix.Length;
            const string MaxInt32 = "2147483647";
            foreach (string alias in _namespaces.Namespaces.Keys)
            {
                string name;
                if (alias.Length > prefixLength)
                {
                    name = alias;
                    if (name.Length > prefixLength && name.Length <= prefixLength + MaxInt32.Length && name.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        bool numeric = true;
                        for (int j = prefixLength; j < name.Length; j++)
                        {
                            if (!Char.IsDigit(name, j))
                            {
                                numeric = false;
                                break;
                            }
                        }
                        if (numeric)
                        {
                            Int64 index = Int64.Parse(name.Substring(prefixLength), NumberStyles.Integer, CultureInfo.InvariantCulture);
                            if (index <= Int32.MaxValue)
                            {
                                Int32 newIndex = (Int32)index;
                                qnIndexes.Add(newIndex);
                            }
                        }
                    }
                }
            }
            if (qnIndexes.Count > 0)
            {
                return qnIndexes;
            }
            return null;
        }

        protected void WriteNullTagEncoded(string name)
        {
            WriteNullTagEncoded(name, null);
        }

        protected void WriteNullTagEncoded(string name, string ns)
        {
            if (name == null || name.Length == 0)
                return;
            WriteStartElement(name, ns, null, true);
            _w.WriteAttributeString("nil", XmlSchema.InstanceNamespace, "true");
            _w.WriteEndElement();
        }

        protected void WriteNullTagLiteral(string name)
        {
            WriteNullTagLiteral(name, null);
        }

        protected void WriteNullTagLiteral(string name, string ns)
        {
            if (name == null || name.Length == 0)
                return;
            WriteStartElement(name, ns, null, false);
            _w.WriteAttributeString("nil", XmlSchema.InstanceNamespace, "true");
            _w.WriteEndElement();
        }

        protected void WriteEmptyTag(string name)
        {
            WriteEmptyTag(name, null);
        }

        protected void WriteEmptyTag(string name, string ns)
        {
            if (name == null || name.Length == 0)
                return;
            WriteStartElement(name, ns, null, false);
            _w.WriteEndElement();
        }

        protected void WriteEndElement()
        {
            _w.WriteEndElement();
        }

        protected void WriteEndElement(object o)
        {
            _w.WriteEndElement();

            if (o != null && _objectsInUse != null)
            {
#if DEBUG
                // use exception in the place of Debug.Assert to avoid throwing asserts from a server process such as aspnet_ewp.exe
                if (!_objectsInUse.Contains(o)) throw new InvalidOperationException(SR.Format(SR.XmlInternalErrorDetails, "missing stack object of type " + o.GetType().FullName));
#endif

                _objectsInUse.Remove(o);
            }
        }

        protected void WriteSerializable(IXmlSerializable serializable, string name, string ns, bool isNullable)
        {
            WriteSerializable(serializable, name, ns, isNullable, true);
        }

        protected void WriteSerializable(IXmlSerializable serializable, string name, string ns, bool isNullable, bool wrapped)
        {
            if (serializable == null)
            {
                if (isNullable) WriteNullTagLiteral(name, ns);
                return;
            }
            if (wrapped)
            {
                _w.WriteStartElement(name, ns);
            }
            serializable.WriteXml(_w);
            if (wrapped)
            {
                _w.WriteEndElement();
            }
        }

        protected void WriteNullableStringEncoded(string name, string ns, string value, XmlQualifiedName xsiType)
        {
            if (value == null)
                WriteNullTagEncoded(name, ns);
            else
                WriteElementString(name, ns, value, xsiType);
        }

        protected void WriteNullableStringLiteral(string name, string ns, string value)
        {
            if (value == null)
                WriteNullTagLiteral(name, ns);
            else
                WriteElementString(name, ns, value, null);
        }


        protected void WriteNullableStringEncodedRaw(string name, string ns, string value, XmlQualifiedName xsiType)
        {
            if (value == null)
                WriteNullTagEncoded(name, ns);
            else
                WriteElementStringRaw(name, ns, value, xsiType);
        }

        protected void WriteNullableStringEncodedRaw(string name, string ns, byte[] value, XmlQualifiedName xsiType)
        {
            if (value == null)
                WriteNullTagEncoded(name, ns);
            else
                WriteElementStringRaw(name, ns, value, xsiType);
        }

        protected void WriteNullableStringLiteralRaw(string name, string ns, string value)
        {
            if (value == null)
                WriteNullTagLiteral(name, ns);
            else
                WriteElementStringRaw(name, ns, value, null);
        }

        protected void WriteNullableStringLiteralRaw(string name, string ns, byte[] value)
        {
            if (value == null)
                WriteNullTagLiteral(name, ns);
            else
                WriteElementStringRaw(name, ns, value, null);
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        protected void WriteNullableQualifiedNameEncoded(string name, string ns, XmlQualifiedName value, XmlQualifiedName xsiType)
        {
            if (value == null)
                WriteNullTagEncoded(name, ns);
            else
                WriteElementQualifiedName(name, ns, value, xsiType);
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        protected void WriteNullableQualifiedNameLiteral(string name, string ns, XmlQualifiedName value)
        {
            if (value == null)
                WriteNullTagLiteral(name, ns);
            else
                WriteElementQualifiedName(name, ns, value, null);
        }

        protected void WriteElementLiteral(XmlNode node, string name, string ns, bool isNullable, bool any)
        {
            if (node == null)
            {
                if (isNullable) WriteNullTagLiteral(name, ns);
                return;
            }
            WriteElement(node, name, ns, isNullable, any);
        }

        private void WriteElement(XmlNode node, string name, string ns, bool isNullable, bool any)
        {
            if (typeof(XmlAttribute).IsAssignableFrom(node.GetType()))
                throw new InvalidOperationException(SR.XmlNoAttributeHere);
            if (node is XmlDocument)
            {
                node = ((XmlDocument)node).DocumentElement;
                if (node == null)
                {
                    if (isNullable) WriteNullTagEncoded(name, ns);
                    return;
                }
            }
            if (any)
            {
                if (node is XmlElement && name != null && name.Length > 0)
                {
                    // need to check against schema
                    if (node.LocalName != name || node.NamespaceURI != ns)
                        throw new InvalidOperationException(SR.Format(SR.XmlElementNameMismatch, node.LocalName, node.NamespaceURI, name, ns));
                }
            }
            else
                _w.WriteStartElement(name, ns);

            node.WriteTo(_w);

            if (!any)
                _w.WriteEndElement();
        }

        protected Exception CreateUnknownTypeException(object o)
        {
            return CreateUnknownTypeException(o.GetType());
        }

        protected Exception CreateUnknownTypeException(Type type)
        {
            if (typeof(IXmlSerializable).IsAssignableFrom(type)) return new InvalidOperationException(SR.Format(SR.XmlInvalidSerializable, type.FullName));
            TypeDesc typeDesc = new TypeScope().GetTypeDesc(type);
            if (!typeDesc.IsStructLike) return new InvalidOperationException(SR.Format(SR.XmlInvalidUseOfType, type.FullName));
            return new InvalidOperationException(SR.Format(SR.XmlUnxpectedType, type.FullName));
        }

        protected Exception CreateMismatchChoiceException(string value, string elementName, string enumValue)
        {
            // Value of {0} mismatches the type of {1}, you need to set it to {2}.
            return new InvalidOperationException(SR.Format(SR.XmlChoiceMismatchChoiceException, elementName, value, enumValue));
        }

        protected Exception CreateUnknownAnyElementException(string name, string ns)
        {
            return new InvalidOperationException(SR.Format(SR.XmlUnknownAnyElement, name, ns));
        }

        protected Exception CreateInvalidChoiceIdentifierValueException(string type, string identifier)
        {
            return new InvalidOperationException(SR.Format(SR.XmlInvalidChoiceIdentifierValue, type, identifier));
        }

        protected Exception CreateChoiceIdentifierValueException(string value, string identifier, string name, string ns)
        {
            // XmlChoiceIdentifierMismatch=Value '{0}' of the choice identifier '{1}' does not match element '{2}' from namespace '{3}'.
            return new InvalidOperationException(SR.Format(SR.XmlChoiceIdentifierMismatch, value, identifier, name, ns));
        }

        protected Exception CreateInvalidEnumValueException(object value, string typeName)
        {
            return new InvalidOperationException(SR.Format(SR.XmlUnknownConstant, value, typeName));
        }

        protected Exception CreateInvalidAnyTypeException(object o)
        {
            return CreateInvalidAnyTypeException(o.GetType());
        }

        protected Exception CreateInvalidAnyTypeException(Type type)
        {
            return new InvalidOperationException(SR.Format(SR.XmlIllegalAnyElement, type.FullName));
        }

        protected void WriteXmlAttribute(XmlNode node)
        {
            WriteXmlAttribute(node, null);
        }

        protected void WriteXmlAttribute(XmlNode node, object container)
        {
            XmlAttribute attr = node as XmlAttribute;
            if (attr == null) throw new InvalidOperationException(SR.XmlNeedAttributeHere);
            if (attr.Value != null)
            {
                if (attr.NamespaceURI == Wsdl.Namespace && attr.LocalName == Wsdl.ArrayType)
                {
                    string dims;
                    XmlQualifiedName qname = TypeScope.ParseWsdlArrayType(attr.Value, out dims, (container is XmlSchemaObject) ? (XmlSchemaObject)container : null);

                    string value = FromXmlQualifiedName(qname, true) + dims;

                    //<xsd:attribute xmlns:q3="s0" wsdl:arrayType="q3:FoosBase[]" xmlns:q4="http://schemas.xmlsoap.org/soap/encoding/" ref="q4:arrayType" />
                    WriteAttribute(Wsdl.ArrayType, Wsdl.Namespace, value);
                }
                else
                {
                    WriteAttribute(attr.Name, attr.NamespaceURI, attr.Value);
                }
            }
        }

        protected void WriteAttribute(string localName, string ns, string value)
        {
            if (value == null) return;
            if (localName == "xmlns" || localName.StartsWith("xmlns:", StringComparison.Ordinal))
            {
                ;
            }
            else
            {
                int colon = localName.IndexOf(':');
                if (colon < 0)
                {
                    if (ns == XmlReservedNs.NsXml)
                    {
                        string prefix = _w.LookupPrefix(ns);
                        if (prefix == null || prefix.Length == 0)
                            prefix = "xml";
                        _w.WriteAttributeString(prefix, localName, ns, value);
                    }
                    else
                    {
                        _w.WriteAttributeString(localName, ns, value);
                    }
                }
                else
                {
                    string prefix = localName.Substring(0, colon);
                    _w.WriteAttributeString(prefix, localName.Substring(colon + 1), ns, value);
                }
            }
        }

        protected void WriteAttribute(string localName, string ns, byte[] value)
        {
            if (value == null) return;
            if (localName == "xmlns" || localName.StartsWith("xmlns:", StringComparison.Ordinal))
            {
                ;
            }
            else
            {
                int colon = localName.IndexOf(':');
                if (colon < 0)
                {
                    if (ns == XmlReservedNs.NsXml)
                    {
                        string prefix = _w.LookupPrefix(ns);
                        if (prefix == null || prefix.Length == 0)
                            prefix = "xml";
                        _w.WriteStartAttribute("xml", localName, ns);
                    }
                    else
                    {
                        _w.WriteStartAttribute(null, localName, ns);
                    }
                }
                else
                {
                    string prefix = _w.LookupPrefix(ns);
                    _w.WriteStartAttribute(prefix, localName.Substring(colon + 1), ns);
                }
                XmlCustomFormatter.WriteArrayBase64(_w, value, 0, value.Length);
                _w.WriteEndAttribute();
            }
        }

        protected void WriteAttribute(string localName, string value)
        {
            if (value == null) return;
            _w.WriteAttributeString(localName, null, value);
        }

        protected void WriteAttribute(string localName, byte[] value)
        {
            if (value == null) return;

            _w.WriteStartAttribute(null, localName, (string)null);
            XmlCustomFormatter.WriteArrayBase64(_w, value, 0, value.Length);
            _w.WriteEndAttribute();
        }

        protected void WriteAttribute(string prefix, string localName, string ns, string value)
        {
            if (value == null) return;
            _w.WriteAttributeString(prefix, localName, null, value);
        }

        protected void WriteValue(string value)
        {
            if (value == null) return;
            _w.WriteString(value);
        }

        protected void WriteValue(byte[] value)
        {
            if (value == null) return;
            XmlCustomFormatter.WriteArrayBase64(_w, value, 0, value.Length);
        }

        protected void WriteStartDocument()
        {
            if (_w.WriteState == WriteState.Start)
            {
                _w.WriteStartDocument();
            }
        }

        protected void WriteElementString(String localName, String value)
        {
            WriteElementString(localName, null, value, null);
        }

        protected void WriteElementString(String localName, String ns, String value)
        {
            WriteElementString(localName, ns, value, null);
        }

        protected void WriteElementString(String localName, String value, XmlQualifiedName xsiType)
        {
            WriteElementString(localName, null, value, xsiType);
        }

        protected void WriteElementString(String localName, String ns, String value, XmlQualifiedName xsiType)
        {
            if (value == null) return;
            if (xsiType == null)
                _w.WriteElementString(localName, ns, value);
            else
            {
                _w.WriteStartElement(localName, ns);
                WriteXsiType(xsiType.Name, xsiType.Namespace);
                _w.WriteString(value);
                _w.WriteEndElement();
            }
        }

        protected void WriteElementStringRaw(String localName, String value)
        {
            WriteElementStringRaw(localName, null, value, null);
        }

        protected void WriteElementStringRaw(String localName, byte[] value)
        {
            WriteElementStringRaw(localName, null, value, null);
        }

        protected void WriteElementStringRaw(String localName, String ns, String value)
        {
            WriteElementStringRaw(localName, ns, value, null);
        }

        protected void WriteElementStringRaw(String localName, String ns, byte[] value)
        {
            WriteElementStringRaw(localName, ns, value, null);
        }

        protected void WriteElementStringRaw(String localName, String value, XmlQualifiedName xsiType)
        {
            WriteElementStringRaw(localName, null, value, xsiType);
        }

        protected void WriteElementStringRaw(String localName, byte[] value, XmlQualifiedName xsiType)
        {
            WriteElementStringRaw(localName, null, value, xsiType);
        }

        protected void WriteElementStringRaw(String localName, String ns, String value, XmlQualifiedName xsiType)
        {
            if (value == null) return;
            _w.WriteStartElement(localName, ns);
            if (xsiType != null)
                WriteXsiType(xsiType.Name, xsiType.Namespace);
            _w.WriteRaw(value);
            _w.WriteEndElement();
        }

        protected void WriteElementStringRaw(String localName, String ns, byte[] value, XmlQualifiedName xsiType)
        {
            if (value == null) return;
            _w.WriteStartElement(localName, ns);
            if (xsiType != null)
                WriteXsiType(xsiType.Name, xsiType.Namespace);
            XmlCustomFormatter.WriteArrayBase64(_w, value, 0, value.Length);
            _w.WriteEndElement();
        }


        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        protected void WriteElementQualifiedName(String localName, XmlQualifiedName value)
        {
            WriteElementQualifiedName(localName, null, value, null);
        }

        protected void WriteElementQualifiedName(string localName, XmlQualifiedName value, XmlQualifiedName xsiType)
        {
            WriteElementQualifiedName(localName, null, value, xsiType);
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        protected void WriteElementQualifiedName(String localName, String ns, XmlQualifiedName value)
        {
            WriteElementQualifiedName(localName, ns, value, null);
        }

        protected void WriteElementQualifiedName(string localName, string ns, XmlQualifiedName value, XmlQualifiedName xsiType)
        {
            if (value == null) return;
            if (value.Namespace == null || value.Namespace.Length == 0)
            {
                WriteStartElement(localName, ns, null, true);
                WriteAttribute("xmlns", "");
            }
            else
                _w.WriteStartElement(localName, ns);
            if (xsiType != null)
                WriteXsiType(xsiType.Name, xsiType.Namespace);
            _w.WriteString(FromXmlQualifiedName(value, false));
            _w.WriteEndElement();
        }










        protected abstract void InitCallbacks();


        protected void TopLevelElement()
        {
            _objectsInUse = new HashSet<object>();
        }

        ///<internalonly/>
        protected void WriteNamespaceDeclarations(XmlSerializerNamespaces xmlns)
        {
            if (xmlns != null)
            {
                foreach (KeyValuePair<string, string> entry in xmlns.Namespaces)
                {
                    string prefix = (string)entry.Key;
                    string ns = (string)entry.Value;
                    if (_namespaces != null)
                    {
                        string oldNs = _namespaces.Namespaces[prefix] as string;
                        if (oldNs != null && oldNs != ns)
                        {
                            throw new InvalidOperationException(SR.Format(SR.XmlDuplicateNs, prefix, ns));
                        }
                    }
                    string oldPrefix = (ns == null || ns.Length == 0) ? null : Writer.LookupPrefix(ns);

                    if (oldPrefix == null || oldPrefix != prefix)
                    {
                        WriteAttribute("xmlns", prefix, null, ns);
                    }
                }
            }
            _namespaces = null;
        }

        private string NextPrefix()
        {
            if (_usedPrefixes == null)
            {
                return _aliasBase + (++_tempNamespacePrefix);
            }
            while (_usedPrefixes.Contains(++_tempNamespacePrefix)) {; }
            return _aliasBase + _tempNamespacePrefix;
        }
    }

    ///<internalonly/>
    public delegate void XmlSerializationWriteCallback(object o);
}
