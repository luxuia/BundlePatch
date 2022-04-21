using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssetStudio
{
    public abstract class Component : EditorExtension, IGetName
    {
        public PPtr<GameObject> m_GameObject;

        public string m_Name
        {
            get
            {
                GameObject go;
                if (m_GameObject.TryGet(out go))
                {
                    return go.m_Name;
                }
                return null;
            }
            set { }
        }

        protected Component(ObjectReader reader) : base(reader)
        {
            m_GameObject = new PPtr<GameObject>(reader);
        }
    }
}
