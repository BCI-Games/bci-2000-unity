using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BCI2000
{
    [Serializable]
    public class ModuleConfiguration
    {
        public string Name;
        public string[] Arguments;

        public ModuleConfiguration(string name)
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

        public string BuildArgumentString() => BuildArgumentString(Name, Arguments.ToList());

        public static string BuildArgumentString(string name, IEnumerable<string> arguments)
        {
            if (arguments == null)
                return "--local";

            if (!arguments.Any(arg => arg is "--local" or "local"))
                arguments.Append("--local");

            return arguments.Aggregate(new StringBuilder(),
                (builder, argument) => {
                    builder.Append(' ');
                    builder.Append(FormatArgumentString(argument));
                    return builder;
                },
                builder => builder.ToString()
            );
        }

        private static string FormatArgumentString(string argument)
        {
            argument = argument.Trim();
            if (!argument.StartsWith("--"))
                argument = "--" + argument;
            
            return argument;
        }
    }


    [Serializable]
    public class EventDefinition: NamedValueDefinition
    {
        public void ThrowExceptionIfFormatInvalid() => ThrowExceptionIfFormatInvalid(Name, BitWidth);
        public static void ThrowExceptionIfFormatInvalid(string name, int bitWidth)
        => ThrowExceptionIfFormatInvalid(name, bitWidth, "event");
    }
    [Serializable]
    public class StateDefinition: NamedValueDefinition
    {
        public void ThrowExceptionIfFormatInvalid() => ThrowExceptionIfFormatInvalid(Name, BitWidth);
        public static void ThrowExceptionIfFormatInvalid(string name, int bitWidth)
        => ThrowExceptionIfFormatInvalid(name, bitWidth, "state");
    }
    
    public abstract class NamedValueDefinition
    {
        public string Name;
        public int BitWidth = 16;
        public uint InitialValue = 0;

        public static void ThrowExceptionIfFormatInvalid
        (
            string name, int bitWidth, string exceptionLabel = "value"
        )
        {
			if (name.Any(char.IsWhiteSpace)) {
				throw new BCI2000ArgumentFormatException(
                    $"Error adding {exceptionLabel} {name},"
                    + $" {exceptionLabel} names must not contain whitespace"
                );
            }
			if (bitWidth > 32 || bitWidth < 1) {
				throw new BCI2000ArgumentFormatException(
                    $"Bit width of {bitWidth} for {exceptionLabel} {name} is invalid."
                    + " Bit width must be between 1 and 32."
                );
            }
        }

        public void DeconstructInto(Action<string, int, uint> action)
        => action(Name, BitWidth, InitialValue);
    }


    [Serializable]
    public class ParameterDefinition
    {
        public string Section = "Application:";
        public string Name;
        public string DefaultValue = "";
        public string MinimumValue = "";
        public string MaximumValue = "";

        private string Coalesce(string s) => string.IsNullOrEmpty(s)? "%": s;

        public void ThrowExceptionIfFormatInvalid()
        => ThrowExceptionIfFormatInvalid(Section, Name, DefaultValue, MinimumValue, MaximumValue);
        public static void ThrowExceptionIfFormatInvalid(
            string section, string name, string defaultValue = "",
            string minimumValue = "", string maximumValue = ""
        )
        {
            string[] arguments = new []{section, name, defaultValue, minimumValue, maximumValue};
			var whitespaceArguments = arguments.Where(str => str.Any(char.IsWhiteSpace));

			if (whitespaceArguments.Count() != 0) {
			    whitespaceArguments = whitespaceArguments.Select(str => $"\"{str}\""); 
				throw new BCI2000ArgumentFormatException(
					"Parameter definition arguments must not contain whitespace."
					+ $" Argument(s) {string.Join(',', whitespaceArguments)} contain whitespace."
				);
			}
        }

        public void DeconstructInto(Action<string, string, string, string, string> action)
        => action(
            Section, Name, Coalesce(DefaultValue),
           Coalesce(MinimumValue), Coalesce(MaximumValue)
        );
    }
}