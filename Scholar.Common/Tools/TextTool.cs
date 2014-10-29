using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Scholar.Common.Tools
{
    public static class TextTool
    {
        static readonly Dictionary<string, string> Words = new Dictionary<string, string>();

        static TextTool()
        {
            Words.Add("а", "a");
            Words.Add("б", "b");
            Words.Add("в", "v");
            Words.Add("г", "g");
            Words.Add("д", "d");
            Words.Add("е", "e");
            Words.Add("ё", "yo");
            Words.Add("ж", "zh");
            Words.Add("з", "z");
            Words.Add("и", "i");
            Words.Add("й", "j");
            Words.Add("к", "k");
            Words.Add("л", "l");
            Words.Add("м", "m");
            Words.Add("н", "n");
            Words.Add("о", "o");
            Words.Add("п", "p");
            Words.Add("р", "r");
            Words.Add("с", "s");
            Words.Add("т", "t");
            Words.Add("у", "u");
            Words.Add("ф", "f");
            Words.Add("х", "h");
            Words.Add("ц", "c");
            Words.Add("ч", "ch");
            Words.Add("ш", "sh");
            Words.Add("щ", "sch");
            Words.Add("ъ", "j");
            Words.Add("ы", "i");
            Words.Add("ь", "j");
            Words.Add("э", "e");
            Words.Add("ю", "yu");
            Words.Add("я", "ya");
            Words.Add("А", "A");
            Words.Add("Б", "B");
            Words.Add("В", "V");
            Words.Add("Г", "G");
            Words.Add("Д", "D");
            Words.Add("Е", "E");
            Words.Add("Ё", "Yo");
            Words.Add("Ж", "Zh");
            Words.Add("З", "Z");
            Words.Add("И", "I");
            Words.Add("Й", "J");
            Words.Add("К", "K");
            Words.Add("Л", "L");
            Words.Add("М", "M");
            Words.Add("Н", "N");
            Words.Add("О", "O");
            Words.Add("П", "P");
            Words.Add("Р", "R");
            Words.Add("С", "S");
            Words.Add("Т", "T");
            Words.Add("У", "U");
            Words.Add("Ф", "F");
            Words.Add("Х", "H");
            Words.Add("Ц", "C");
            Words.Add("Ч", "Ch");
            Words.Add("Ш", "Sh");
            Words.Add("Щ", "Sch");
            Words.Add("Ъ", "J");
            Words.Add("Ы", "I");
            Words.Add("Ь", "J");
            Words.Add("Э", "E");
            Words.Add("Ю", "Yu");
            Words.Add("Я", "Ya");
        }

        /// <summary>
        /// Транслит русского текста
        /// </summary>
        /// <param name="russianString"></param>
        /// <returns></returns>
        public static string TransformToLat(string russianString)
        {
            return Words.Aggregate(russianString, (current, pair) => current.Replace(pair.Key, pair.Value));
        }

        public static string TransformToKir(string englishString)
        {
            return Words.Aggregate(englishString, (current, pair) => current.Replace(pair.Value, pair.Key));
        }

        public static bool IsEditionChar(char c)
        {
            return char.IsLetter(c) || char.IsDigit(c) || c == ' ';
        }

        public static bool IsNameChar(char c)
        {
            return char.IsLetter(c) || char.IsDigit(c) || c == ' ' || c == ',' || c == '.' || c == '-' || c == ';';
        }

        public static string ClearNameFromGrade(string name)
        {
            var starts = new[]
            {
                "BS, ", "B.S., ", "BSc, ", "B.Sc., ",
                "BA, ", "B.A., ",

                "MS, ", "M.S., ", "MSc, ", "M.Sc., " ,
                "MA, ", "M.A., ", "MD, ", "M.D., " ,
                "MBA, ", "M.B.A., ",

                "PhD, ", "Ph.D., ",

                "FRCP, ", "F.R.C.P., ", "FRCPCH, ", "F.R.C.P.C.H., "
            };

            var ends = new[]
            {
                ", BS", ", B.S.", ", BS", ", B.S.",
                ", BA", ", B.A.",

                ", MS", ", M.S.", ", MSc", ", M.Sc." , 
                ", MA", ", M.A.", ", MD", ", M.D.", 
                ", MBA", ", M.B.A.", 

                ", PhD", ", Ph.D.",

                ", FRCP", ", F.R.C.P.", ", FRCPCH", ", F.R.C.P.C.H."
            };

            var foundStart = starts.FirstOrDefault(i => name.ToLower().StartsWith(i.ToLower()));
            if (foundStart != null)
            {
                name = name.Remove(0, foundStart.Length);
            }
            else
            {
                var foundEnd = ends.FirstOrDefault(i => name.ToLower().EndsWith(i.ToLower()));
                if (foundEnd != null)
                {
                    name = name.Remove(name.Length - foundEnd.Length);
                }
            }

            return name;
        }

        public static string[] GetName(string name)
        {
            name = ClearNameFromGrade(name);

            var nameValues = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (nameValues.Length != 2 && nameValues.Length != 3 && nameValues.Length != 4)
            {
                return null;
            }

            var firstNameIndex = 0;
            var middleNameIndex = 1;
            var lastNameIndex = 2;
            var removeLast = false;

            if (nameValues[0].EndsWith(","))
            {
                lastNameIndex = 0;
                firstNameIndex = 1;
                middleNameIndex = 2;

                removeLast = true;
            }

            var initials = nameValues[firstNameIndex][0].ToString(CultureInfo.InvariantCulture);
            if (nameValues.Length >= 3)
            {
                initials += nameValues[middleNameIndex][0].ToString(CultureInfo.InvariantCulture);
                if (nameValues.Length == 4)
                {
                    if (char.IsUpper(nameValues[middleNameIndex + 1][0])) // For JP de Pablo
                    {
                        initials += nameValues[middleNameIndex + 1][0].ToString(CultureInfo.InvariantCulture);
                    }

                    if (lastNameIndex != 0)
                    {
                        lastNameIndex++;
                    }
                }
            }
            else if (lastNameIndex != 0)
            {
                lastNameIndex = 1;
            }

            var lastName = nameValues[lastNameIndex];
            if (removeLast)
            {
                lastName = lastName.Remove(lastName.Length - 1);
            }

            return new[] { initials.ToUpper(), lastName };
        }

        public static string[] GetNames(string lastName, string names)
        {
            var index = names.ToLower().IndexOf(lastName.ToLower(), StringComparison.Ordinal);
            var startIndex = index;
            var endIndex = (index + lastName.Length > names.Length) ? names.Length - 1 : index + lastName.Length;

            while (startIndex > 0 && (IsNameChar(names[startIndex])))
            {
                startIndex--;
            }

            while (endIndex < names.Length && (IsNameChar(names[endIndex])))
            {
                endIndex++;
            }

            if (startIndex == index)
            {
                startIndex--;
            }

            if (endIndex == index + lastName.Length)
            {
                endIndex++;
            }

            if (startIndex < 0)
            {
                startIndex = 0;
            }

            if (endIndex > names.Length)
            {
                endIndex = names.Length;
            }

            var justNames = names.Substring(startIndex, endIndex - startIndex);
            if (!char.IsLetter(justNames[0]))
            {
                justNames = justNames.Remove(0, 1);
            }

            return justNames.Split(justNames.Contains(";") ? ';' : ',').Select(i => i.Trim()).ToArray();
        }

        public static string[] RussianLastNames()
        {
            if (!File.Exists("RussianLastNames.txt"))
                return new string[0];

            return File.ReadAllLines("RussianLastNames.txt")
                .Where(i => i != null)
                .Select(i => i.Trim().ToLower())
                .Where(i => !string.IsNullOrEmpty(i))
                .ToArray();
        }

        public static string[] GetModifiedArray(string[] initial)
        {
            var modified = new List<string>();

            for (var i = 0; i < initial.Length; i++)
            {
                if (initial[i].StartsWith("\""))
                {
                    var endPosition = -1;
                    var builder = new StringBuilder();
                    for (var j = i + 1; j < initial.Length; j++)
                    {
                        if (initial[j].EndsWith("\""))
                        {
                            for (var k = i; k <= j; k++)
                            {
                                builder.AppendFormat("{0} ", initial[k]);
                            }

                            builder.Remove(0, 1);
                            builder.Remove(builder.Length - 2, 2);
                            endPosition = j;
                            break;
                        }
                    }

                    if (builder.Length > 0)
                    {
                        modified.Add(builder.ToString());
                        i = endPosition;
                    }
                }
                else
                    modified.Add(initial[i]);
            }

            return modified.ToArray();
        }
    }
}
