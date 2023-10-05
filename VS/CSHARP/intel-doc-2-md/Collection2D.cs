using System.Text;

namespace intel_doc_2_md
{
    public class Collection2D
    {
        readonly List<(float, float, List<char>)> data;

       public Collection2D()
        {
            this.data = new List<(float, float, List<char>)> ();
        }

        public void Add(float x, float y, string str) 
        {
            List<char> str2 = new(str.Length);
            for (int i = 0; i < str.Length; ++i)
            {
                str2[i] = str[i];
            }
            this.data.Add((x, y, str2));
        }

        private static void PlaceChar(char c, int pos, ref List<char> line)
        {
            while (line.Count < (pos-1))
            {
                line.Add(' ');
            }
            if (line.Count < pos)
            {
                line.Add(c);
            } else
            {
                if (line[pos] == ' ')
                {
                    line[pos] = c;
                }
                else
                {
                    Console.WriteLine($"WARNING: PlaceChar: going to place \'{c}\' at pos {pos}, but there is already a char \'{line[pos]}\'");
                }
            }
        }

        public string Print(int scale)
        {
            Dictionary<int, List<char>> str_2d = new();

            foreach ((float x, float y, List<char> str) in this.data)
            {
                int lineNumber = (int)Math.Round(x * scale);
                int cPos = (int)Math.Round(y * scale);

                List<char> line = new();
                if (str_2d.TryGetValue(lineNumber, out List<char>? value)) {
                    line = value;
                }                
                for (int i = 0; i<str.Count; ++i)
                {
                    PlaceChar(str[i], cPos + i, ref line);
                }
                str_2d[lineNumber] = line;
            }

            int max = 0;
            foreach ((int lineNumber, _) in str_2d)
            {
                if (lineNumber > max)
                {
                    max = lineNumber;
                }
            }

            StringBuilder sb = new();
            for (int i = 0; i < max; ++i)
            {
                if (str_2d.TryGetValue(i, out List<char>? value))
                {
                    foreach (char c in value)
                    {
                        sb.Append(c);
                    }
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
