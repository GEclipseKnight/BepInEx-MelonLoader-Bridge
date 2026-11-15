using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MelonLoader.ICSharpCode.SharpZipLib.Core
{
	public class NameFilter : IScanFilter
	{
		#region Constructors

		public NameFilter(string filter)
		{
			filter_ = filter;
			inclusions_ = new List<Regex>();
			exclusions_ = new List<Regex>();
			Compile();
		}

		#endregion Constructors

		public static bool IsValidExpression(string expression)
		{
			bool result = true;
			try
			{
				var exp = new Regex(expression, RegexOptions.IgnoreCase | RegexOptions.Singleline);
			}
			catch (ArgumentException)
			{
				result = false;
			}
			return result;
		}

		public static bool IsValidFilterExpression(string toTest)
		{
			bool result = true;

			try
			{
				if (toTest != null)
				{
					string[] items = SplitQuoted(toTest);
					for (int i = 0; i < items.Length; ++i)
					{
						if ((items[i] != null) && (items[i].Length > 0))
						{
							string toCompile;

							if (items[i][0] == '+')
							{
								toCompile = items[i].Substring(1, items[i].Length - 1);
							}
							else if (items[i][0] == '-')
							{
								toCompile = items[i].Substring(1, items[i].Length - 1);
							}
							else
							{
								toCompile = items[i];
							}

							var testRegex = new Regex(toCompile, RegexOptions.IgnoreCase | RegexOptions.Singleline);
						}
					}
				}
			}
			catch (ArgumentException)
			{
				result = false;
			}

			return result;
		}

		public static string[] SplitQuoted(string original)
		{
			char escape = '\\';
			char[] separators = { ';' };

			var result = new List<string>();

			if (!string.IsNullOrEmpty(original))
			{
				int endIndex = -1;
				var b = new StringBuilder();

				while (endIndex < original.Length)
				{
					endIndex += 1;
					if (endIndex >= original.Length)
					{
						result.Add(b.ToString());
					}
					else if (original[endIndex] == escape)
					{
						endIndex += 1;
						if (endIndex >= original.Length)
						{
							throw new ArgumentException("Missing terminating escape character", nameof(original));
						}
						if (Array.IndexOf(separators, original[endIndex]) < 0)
							b.Append(escape);

						b.Append(original[endIndex]);
					}
					else
					{
						if (Array.IndexOf(separators, original[endIndex]) >= 0)
						{
							result.Add(b.ToString());
							b.Length = 0;
						}
						else
						{
							b.Append(original[endIndex]);
						}
					}
				}
			}

			return result.ToArray();
		}

		public override string ToString()
		{
			return filter_;
		}

		public bool IsIncluded(string name)
		{
			bool result = false;
			if (inclusions_.Count == 0)
			{
				result = true;
			}
			else
			{
				foreach (Regex r in inclusions_)
				{
					if (r.IsMatch(name))
					{
						result = true;
						break;
					}
				}
			}
			return result;
		}

		public bool IsExcluded(string name)
		{
			bool result = false;
			foreach (Regex r in exclusions_)
			{
				if (r.IsMatch(name))
				{
					result = true;
					break;
				}
			}
			return result;
		}

		#region IScanFilter Members

		public bool IsMatch(string name)
		{
			return (IsIncluded(name) && !IsExcluded(name));
		}

		#endregion IScanFilter Members

		private void Compile()
		{
			if (filter_ == null)
			{
				return;
			}

			string[] items = SplitQuoted(filter_);
			for (int i = 0; i < items.Length; ++i)
			{
				if ((items[i] != null) && (items[i].Length > 0))
				{
					bool include = (items[i][0] != '-');
					string toCompile;

					if (items[i][0] == '+')
					{
						toCompile = items[i].Substring(1, items[i].Length - 1);
					}
					else if (items[i][0] == '-')
					{
						toCompile = items[i].Substring(1, items[i].Length - 1);
					}
					else
					{
						toCompile = items[i];
					}

					if (include)
					{
						inclusions_.Add(new Regex(toCompile, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline));
					}
					else
					{
						exclusions_.Add(new Regex(toCompile, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline));
					}
				}
			}
		}

		#region Instance Fields

		private string filter_;
		private List<Regex> inclusions_;
		private List<Regex> exclusions_;

		#endregion Instance Fields
	}
}
