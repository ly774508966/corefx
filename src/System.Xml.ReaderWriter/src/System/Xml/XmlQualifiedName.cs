// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Diagnostics;

namespace System.Xml
{
    /// <devdoc>
    ///    <para>[To be supplied.]</para>
    /// </devdoc>
    public class XmlQualifiedName
    {
        private string _name;
        private string _ns;

        private Int32 _hash;

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public static readonly XmlQualifiedName Empty = new XmlQualifiedName(string.Empty);

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public XmlQualifiedName() : this(string.Empty, string.Empty) { }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public XmlQualifiedName(string name) : this(name, string.Empty) { }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public XmlQualifiedName(string name, string ns)
        {
            _ns = ns == null ? string.Empty : ns;
            _name = name == null ? string.Empty : name;
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public string Namespace
        {
            get { return _ns; }
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public string Name
        {
            get { return _name; }
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public override int GetHashCode()
        {
            if (_hash == 0)
            {
                _hash = Name.GetHashCode() /*+ Namespace.GetHashCode()*/; // for perf reasons we are not taking ns's hashcode.
            }
            return _hash;
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public bool IsEmpty
        {
            get { return Name.Length == 0 && Namespace.Length == 0; }
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public override string ToString()
        {
            return Namespace.Length == 0 ? Name : string.Concat(Namespace, ":", Name);
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public override bool Equals(object other)
        {
            XmlQualifiedName qname;

            if ((object)this == other)
            {
                return true;
            }

            qname = other as XmlQualifiedName;
            if (qname != null)
            {
                return (Name == qname.Name && Namespace == qname.Namespace);
            }
            return false;
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public static bool operator ==(XmlQualifiedName a, XmlQualifiedName b)
        {
            if ((object)a == (object)b)
                return true;

            if ((object)a == null || (object)b == null)
                return false;

            return (a.Name == b.Name && a.Namespace == b.Namespace);
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public static bool operator !=(XmlQualifiedName a, XmlQualifiedName b)
        {
            return !(a == b);
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public static string ToString(string name, string ns)
        {
            return ns == null || ns.Length == 0 ? name : ns + ":" + name;
        }
    }
}

