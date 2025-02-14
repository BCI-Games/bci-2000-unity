using System;

namespace BCI2000
{
    [Serializable]
    public class BCI2000Module
    {
        public string Name;
        public string[] Arguments;

        public BCI2000Module(string name)
        {
            Name = name;
            Arguments = new string[0];
        }

        public void Deconstruct
        (
            out string name, out string[] arguments
        )
        {
            name = Name;
            arguments = Arguments;
        }
    }


    [Serializable]
    public class BCI2000EventDefinition: NamedValueDefinition {}
    [Serializable]
    public class BCI2000StateDefinition: NamedValueDefinition {}
    
    public class NamedValueDefinition
    {
        public string Name;
        public int BitWidth = 16;
        public uint InitialValue = 0;

        public void Deconstruct
        (
            out string name, out int bitWidth, out uint initialValue
        )
        {
            name = Name;
            bitWidth= BitWidth;
            initialValue = InitialValue;
        }
    }


    [Serializable]
    public class BCI2000ParameterDefinition
    {
        public string Section = "Application:";
        public string Name;
        public string DefaultValue = "";
        public string MinimumValue = "";
        public string MaximumValue = "";

        public void Deconstruct
        (
            out string section, out string name,
            out string defaultValue,
            out string minimumValue, out string maximumValue
        )
        {
            section = Section;
            name = Name;
            defaultValue = DefaultValue;
            minimumValue = MinimumValue;
            maximumValue = MaximumValue;
        }
    }
}