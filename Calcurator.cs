using System;
using System.Globalization;

namespace CalculatorLib
{
    // Note: filename is Calcurator.cs per request (misspelled).
    public static class Calcurator
    {
        // Adds any number of operands.
        public static double Add(params double[] values)
        {
            double sum = 0;
            if (values == null) return sum;
            foreach (var v in values) sum += v;
            return sum;
        }

        // Subtract b from a.
        public static double Subtract(double a, double b) => a - b;

        // Multiplies any number of operands. Empty -> 0, single -> that value.
        public static double Multiply(params double[] values)
        {
            if (values == null || values.Length == 0) return 0;
            double product = 1;
            foreach (var v in values) product *= v;
            return product;
        }

        // Divides a by b. Throws DivideByZeroException for b == 0.
        public static double Divide(double a, double b)
        {
            if (b == 0) throw new DivideByZeroException("Denominator is zero.");
            return a / b;
        }

        // Safe divide that returns false when denominator is zero.
        public static bool TryDivide(double a, double b, out double result)
        {
            if (b == 0)
            {
                result = default;
                return false;
            }
            result = a / b;
            return true;
        }

        // Parses a simple binary expression like "3 + 4" or "-2.5 * 4".
        // Supported operators: + - * /
        // Returns true if parsed and computed successfully.
        public static bool TryEvaluate(string expression, out double value)
        {
            value = default;
            if (string.IsNullOrWhiteSpace(expression)) return false;

            // Split on whitespace to keep parsing simple: "<left> <op> <right>"
            var parts = expression.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3) return false;

            if (!double.TryParse(parts[0], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var left))
                return false;
            if (!double.TryParse(parts[2], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var right))
                return false;

            var op = parts[1];
            try
            {
                value = op switch
                {
                    "+" => Add(left, right),
                    "-" => Subtract(left, right),
                    "*" => Multiply(left, right),
                    "/" => Divide(left, right),
                    _ => throw new InvalidOperationException($"Unsupported operator '{op}'.")
                };
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}