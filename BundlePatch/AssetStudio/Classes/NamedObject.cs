using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssetStudio
{
    public interface IGetName
    {
        public string m_Name { get; set; }
    }

    public class NamedObject : EditorExtension, IGetName
    {
        public string m_Name { get; set; }

        protected NamedObject(ObjectReader reader) : base(reader)
        {
            m_Name = reader.ReadAlignedString();
        }
    }
}
