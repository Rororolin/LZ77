using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

class LZ77
{
    const int MinSize     = 1;
    const int MaxSize     = 64;   // ограничение на словарь и буфер
    const int MaxInputLen = 1000; // ограничение на длину входной строки

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        Console.WriteLine("Алгоритм LZ77");
        Console.WriteLine("=".PadRight(70, '='));
        Console.WriteLine("1 - Кодировать строку");
        Console.WriteLine("2 - Декодировать битовый код");
        Console.Write("Выберите режим (1/2): ");
        string mode = Console.ReadLine()?.Trim() ?? "";

        if (mode == "1")
        {
            RunEncode();
        }
        else if (mode == "2")
        {
            RunDecode();
        }
        else
        {
            Console.WriteLine("Неизвестный режим.");
        }
    }

    static void RunEncode()
    {
        int dictSize   = ReadIntInRange($"Введите размер словаря  ({MinSize}..{MaxSize}): ", MinSize, MaxSize);
        int bufferSize = ReadIntInRange($"Введите размер буфера   ({MinSize}..{MaxSize}): ", MinSize, MaxSize);

        Console.Write($"Введите строку для кодирования (до {MaxInputLen} символов): ");
        string input = Console.ReadLine() ?? "";

        if (string.IsNullOrEmpty(input))
        {
            Console.WriteLine("Ошибка: строка не может быть пустой!");
            return;
        }
        if (string.IsNullOrWhiteSpace(input))
        {
            Console.WriteLine("Ошибка: строка не может состоять только из пробелов!");
            return;
        }
        if (input.Length > MaxInputLen)
        {
            Console.WriteLine($"Ошибка: строка длиннее {MaxInputLen} символов ({input.Length}).");
            return;
        }

        Console.WriteLine();
        Encode(input, dictSize, bufferSize);
    }

    static void RunDecode()
    {
        int dictSize   = ReadIntInRange($"Введите размер словаря  ({MinSize}..{MaxSize}): ", MinSize, MaxSize);
        int bufferSize = ReadIntInRange($"Введите размер буфера   ({MinSize}..{MaxSize}): ", MinSize, MaxSize);

        Console.Write("Введите уникальные символы в порядке таблицы кодов (без пробелов, например авдеиклнопстч_): ");
        string alphabet = Console.ReadLine() ?? "";
        if (string.IsNullOrEmpty(alphabet))
        {
            Console.WriteLine("Ошибка: алфавит не может быть пустым!");
            return;
        }
        // проверка на повторы
        var dup = alphabet.GroupBy(c => c).FirstOrDefault(g => g.Count() > 1);
        if (dup != null)
        {
            Console.WriteLine($"Ошибка: символ '{dup.Key}' встречается в алфавите {dup.Count()} раз — должен быть уникальным.");
            return;
        }

        Console.Write("Введите битовый код (можно с пробелами): ");
        string rawBits = Console.ReadLine() ?? "";
        // убираем пробельные символы
        var sbBits = new StringBuilder();
        foreach (var c in rawBits)
        {
            if (char.IsWhiteSpace(c)) continue;
            if (c != '0' && c != '1')
            {
                Console.WriteLine($"Ошибка: в битовом коде встретился недопустимый символ '{c}'. Разрешены только 0 и 1.");
                return;
            }
            sbBits.Append(c);
        }
        string bits = sbBits.ToString();
        if (bits.Length == 0)
        {
            Console.WriteLine("Ошибка: пустой код!");
            return;
        }

        Console.WriteLine();
        Decode(bits, alphabet, dictSize, bufferSize);
    }

    // Считывает целое число в диапазоне [min..max]; отклоняет буквы, дроби, выход за границы
    static int ReadIntInRange(string prompt, int min, int max)
    {
        while (true)
        {
            Console.Write(prompt);
            string line = Console.ReadLine()?.Trim() ?? "";

            if (int.TryParse(line, out int value) && value >= min && value <= max)
                return value;

            Console.WriteLine($"  Ошибка: нужно целое число в диапазоне {min}..{max}.");
        }
    }

    // Минимальное количество бит, чтобы закодировать count различных значений (0..count-1)
    static int CalcBits(int count)
    {
        if (count <= 1) return 1;
        int bits = 0;
        int n = count - 1;
        while (n > 0) { bits++; n >>= 1; }
        return bits;
    }

    // Единая ширина поля для l, d и кода символа
    static int FieldWidth(int alphabetSize, int dictSize, int bufferSize)
    {
        int charBits = CalcBits(alphabetSize);
        int lBits    = CalcBits(bufferSize + 1);
        int dBits    = CalcBits(dictSize);
        return Math.Max(charBits, Math.Max(lBits, dBits));
    }

    static void Encode(string input, int dictSize, int bufferSize)
    {
        // Шаг 1: уникальные символы — сначала буквы по алфавиту, затем спецсимволы, пробел последним
        var uniqueChars = input.Distinct()
            .OrderBy(c => c == ' ' ? 2 : char.IsLetter(c) ? 0 : 1)
            .ThenBy(c => c)
            .ToList();

        int fw = FieldWidth(uniqueChars.Count, dictSize, bufferSize);
        int charBits = fw, lBits = fw, dBits = fw;

        var charToCode = new Dictionary<char, string>();

        Console.WriteLine($"Шаг 1. Кодируем уникальные символы строки ({uniqueChars.Count} шт., по {fw} бит):");
        for (int i = 0; i < uniqueChars.Count; i++)
        {
            string code = Convert.ToString(i, 2).PadLeft(fw, '0');
            charToCode[uniqueChars[i]] = code;
            Console.WriteLine($"  '{uniqueChars[i]}' = {code}");
        }
        Console.WriteLine();

        // Инициализация словаря
        char[] dictionary = new char[dictSize];
        for (int i = 0; i < dictSize; i++) dictionary[i] = 'ε';

        int pos  = 0;
        int step = 0;
        var resultCodes = new List<(string l, string d, string c)>();

        Console.WriteLine("Шаг 2. Таблица кодирования:");
        Console.WriteLine();
        Console.WriteLine($"{"j",-6} {"Словарь",-22} {"Буфер",-22} {"l",-8} {"d",-8} {"символ"}");
        Console.WriteLine(new string('-', 75));

        while (pos < input.Length)
        {
            // Формируем буфер
            char[] buffer = new char[bufferSize];
            for (int i = 0; i < bufferSize; i++)
                buffer[i] = (pos + i < input.Length) ? input[pos + i] : 'ε';

            // Ищем максимальное совпадение в словаре
            int maxLength = 0;
            int maxOffset = 0;
            string dictStr = new string(dictionary);

            // Длину совпадения ограничиваем так, чтобы остался хотя бы 1 символ под литерал
            int maxMatchLen = Math.Min(bufferSize, input.Length - pos - 1);
            for (int length = 1; length <= maxMatchLen; length++)
            {
                string pattern = new string(buffer, 0, length);
                if (pattern.Contains('ε')) break;

                bool found = false;
                for (int offset = 0; offset <= dictSize - length; offset++)
                {
                    if (dictStr.Substring(offset, length) == pattern)
                    {
                        maxLength = length;
                        maxOffset = offset;
                        found = true;
                        break;
                    }
                }
                if (!found) break;
            }

            // Следующий символ после совпадения
            char   nextChar;
            string nextCharDisplay;
            string lCode, dCode, charCode;

            if (maxLength > 0)
            {
                int ncp = pos + maxLength;
                if (ncp < input.Length)
                {
                    nextChar        = input[ncp];
                    nextCharDisplay = nextChar.ToString();
                }
                else
                {
                    nextChar        = '\0';
                    nextCharDisplay = "EOF";
                }
                lCode = Convert.ToString(maxLength, 2).PadLeft(lBits, '0');
                dCode = Convert.ToString(maxOffset, 2).PadLeft(dBits, '0');
            }
            else
            {
                nextChar        = input[pos];
                nextCharDisplay = nextChar.ToString();
                lCode = new string('0', lBits);
                dCode = new string('0', dBits);
            }

            charCode = (nextChar != '\0' && charToCode.TryGetValue(nextChar, out string? mappedCode))
                ? mappedCode
                : new string('0', charBits);

            string dictDisplay   = new string(dictionary);
            string bufferDisplay = new string(buffer);
            Console.WriteLine($"{step,-6} {dictDisplay,-22} {bufferDisplay,-22} {lCode,-8} {dCode,-8} {charCode} ({nextCharDisplay})");

            resultCodes.Add((lCode, dCode, charCode));

            // Сдвиг
            int shift = maxLength + 1;
            for (int i = 0; i < shift && pos < input.Length; i++)
            {
                for (int j = 0; j < dictSize - 1; j++) dictionary[j] = dictionary[j + 1];
                dictionary[dictSize - 1] = input[pos];
                pos++;
            }
            step++;
        }

        // Итоговая битовая строка
        var sb = new StringBuilder();
        foreach (var (l, d, c) in resultCodes) { sb.Append(l); sb.Append(d); sb.Append(c); }
        string bits = sb.ToString();

        // Перевод в шестнадцатеричную систему (дополняем слева нулями до кратности 4)
        int padLen     = (4 - bits.Length % 4) % 4;
        string padded  = bits.PadLeft(bits.Length + padLen, '0');
        var hexSb      = new StringBuilder();
        for (int i = 0; i < padded.Length; i += 4)
            hexSb.Append(Convert.ToInt32(padded.Substring(i, 4), 2).ToString("X"));

        // Итог
        Console.WriteLine();
        Console.WriteLine("Результат кодирования:");
        Console.WriteLine($"  Уникальных символов : {uniqueChars.Count}  → {charBits} бит на символ  (2^{charBits} = {1 << charBits})");
        Console.WriteLine($"  Размер словаря      : {dictSize}");
        Console.WriteLine($"  Размер буфера       : {bufferSize}");
        Console.WriteLine($"  Алфавит (для декода): {new string(uniqueChars.ToArray())}");
        Console.WriteLine($"  Битовая строка      : {bits}");
        Console.WriteLine($"  Результат (hex): 0x{hexSb}");
    }

    static void Decode(string bits, string alphabet, int dictSize, int bufferSize)
    {
        // Восстанавливаем таблицу кодов символов
        int fw = FieldWidth(alphabet.Length, dictSize, bufferSize);
        int tripleLen = fw * 3;

        if (bits.Length % tripleLen != 0)
        {
            Console.WriteLine($"Ошибка: длина битовой строки ({bits.Length}) не кратна {tripleLen} (3 поля по {fw} бит).");
            return;
        }

        var codeToChar = new Dictionary<string, char>();
        Console.WriteLine($"Восстановленная таблица кодов ({fw} бит на поле):");
        for (int i = 0; i < alphabet.Length; i++)
        {
            string code = Convert.ToString(i, 2).PadLeft(fw, '0');
            codeToChar[code] = alphabet[i];
            Console.WriteLine($"  {code} = '{alphabet[i]}'");
        }
        Console.WriteLine();

        char[] dictionary = new char[dictSize];
        for (int i = 0; i < dictSize; i++) dictionary[i] = 'ε';

        var result = new StringBuilder();
        int step = 0;

        Console.WriteLine("Шаги декодирования:");
        Console.WriteLine($"{"j",-6} {"Словарь",-22} {"l",-8} {"d",-8} {"символ",-10} {"кусок"}");
        Console.WriteLine(new string('-', 75));

        for (int i = 0; i < bits.Length; i += tripleLen)
        {
            string lBitsStr = bits.Substring(i, fw);
            string dBitsStr = bits.Substring(i + fw, fw);
            string cBitsStr = bits.Substring(i + 2 * fw, fw);

            int l = Convert.ToInt32(lBitsStr, 2);
            int d = Convert.ToInt32(dBitsStr, 2);

            if (l > bufferSize)
            {
                Console.WriteLine($"Ошибка на шаге {step}: длина совпадения l={l} превышает размер буфера {bufferSize}.");
                return;
            }
            if (l > 0 && d >= dictSize)
            {
                Console.WriteLine($"Ошибка на шаге {step}: смещение d={d} выходит за границы словаря (размер {dictSize}).");
                return;
            }
            if (!codeToChar.TryGetValue(cBitsStr, out char ch))
            {
                Console.WriteLine($"Ошибка на шаге {step}: код символа {cBitsStr} не найден в алфавите.");
                return;
            }

            // Собираем совпавшую подстроку из словаря
            var matched = new List<char>();
            for (int k = 0; k < l; k++)
            {
                int idx = d + k;
                if (idx < dictSize)
                    matched.Add(dictionary[idx]);
                else
                    matched.Add(matched[idx - dictSize]);
            }
            string piece = new string(matched.ToArray()) + ch;

            string dictDisplay = new string(dictionary);
            Console.WriteLine($"{step,-6} {dictDisplay,-22} {lBitsStr,-8} {dBitsStr,-8} {cBitsStr + " (" + ch + ")",-10} {piece}");

            result.Append(piece);

            // Сдвигаем словарь
            foreach (var c in piece)
            {
                for (int k = 0; k < dictSize - 1; k++) dictionary[k] = dictionary[k + 1];
                dictionary[dictSize - 1] = c;
            }
            step++;
        }

        Console.WriteLine();
        Console.WriteLine("Результат декодирования:");
        Console.WriteLine($"  {result}");
    }
}
