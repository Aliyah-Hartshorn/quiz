/*
 * Math Quiz
 * =========
 *
 * A single-file C# console application that generates randomized, timed
 * math questions spanning fourteen topics, from basic arithmetic through
 * very advanced material: Arithmetic, Number Theory, Algebra, Precalculus,
 * Geometry, Trigonometry, Combinatorics, Statistics, Complex Numbers,
 * Sequences & Series, Calculus, Multivariable Calculus, Differential
 * Equations, and Linear Algebra. Difficulty increases as the player
 * progresses through a round, both within each topic and, taken as a whole,
 * across the topic list.
 *
 * Features: a full menu system (Start Quiz, Instructions, View Statistics,
 * Exit), difficulty selection (Easy / Medium / Hard / Mixed), hints, a points
 * system with streak bonuses, quiz results display, high-score tracking,
 * per-category statistics with file persistence, and review summaries for
 * missed questions.
 *
 * Everything, including the topic list, lives in this one file. It uses only
 * the .NET base class library.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace MathQuiz
{
    // -----------------------------------------------------------------------
    // Data model
    // -----------------------------------------------------------------------

    /// <summary>A single generated question, its correct answer, time budget, hint, and explanation.</summary>
    public sealed class Question
    {
        public string Prompt { get; init; } = "";
        public double Answer { get; init; }
        public double Tolerance { get; init; }
        public int TimeLimitSeconds { get; init; }
        public string Hint { get; init; } = "";
        public string Explanation { get; init; } = "";
        public int Points { get; init; }
    }

    /// <summary>Aggregate results from a single quiz run.</summary>
    public sealed class QuizResult
    {
        public int Correct { get; init; }
        public int Questions { get; init; }
        public int Points { get; init; }
        public int BestStreak { get; init; }
        public double AverageTime { get; init; }
        public string Category { get; init; } = "";
        public string Difficulty { get; init; } = "";
    }

    /// <summary>Per-category running statistics.</summary>
    public sealed class CategoryStats
    {
        public int Correct { get; set; }
        public int Attempted { get; set; }
    }

    public static class Program
    {
        // -------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------

        private static readonly Random Randomizer = new Random();

        private const string StatisticsFile = "mathquiz_stats.txt";

        private static int highScore;
        private static int totalCorrect;
        private static int totalQuestions;
        private static int gamesPlayed;
        private static readonly Dictionary<string, CategoryStats> Statistics = new Dictionary<string, CategoryStats>();

        // Topic names in file order (order matters for menus and difficulty), plus each topic's generator pool.
        private static List<string> categoryNames = new List<string>();
        private static Dictionary<string, List<Func<Question>>> categories = new Dictionary<string, List<Func<Question>>>();

        // -------------------------------------------------------------------
        // Entry point and main loop
        // -------------------------------------------------------------------

        public static void Main()
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Console.OutputEncoding = Encoding.UTF8;

            BuildCategories();
            LoadStatistics();

            bool running = true;
            while (running)
            {
                ClearScreen();
                ShowTitle();

                Console.WriteLine("1) Start Quiz");
                Console.WriteLine("2) Instructions");
                Console.WriteLine("3) View Statistics");
                Console.WriteLine("4) Exit");
                Console.WriteLine();

                int choice = ReadInt("Choose an option: ", 1, 4);

                switch (choice)
                {
                    case 1: StartQuiz(); break;
                    case 2: ShowInstructions(); break;
                    case 3: ShowStatistics(); break;
                    case 4: running = false; break;
                }
            }

            SaveStatistics();

            ClearScreen();
            Console.WriteLine("=================================");
            Console.WriteLine("        Thanks for playing!");
            Console.WriteLine("=================================");
        }

        // -------------------------------------------------------------------
        // Main menu and setup
        // -------------------------------------------------------------------

        private static void ShowTitle()
        {
            Console.WriteLine("=================================");
            Console.WriteLine("            MATH QUIZ");
            Console.WriteLine("=================================");
            Console.WriteLine("Arithmetic | Number Theory | Algebra | Precalculus");
            Console.WriteLine("Geometry | Trigonometry | Combinatorics | Statistics");
            Console.WriteLine("Complex Numbers | Sequences & Series | Calculus");
            Console.WriteLine("Multivariable Calculus | Differential Equations | Linear Algebra");
            Console.WriteLine();
        }

        private static void StartQuiz()
        {
            ClearScreen();
            ShowTitle();

            string category = AskForCategory();
            string difficulty = AskForDifficulty();
            int questionCount = AskForQuestionCount();

            ClearScreen();
            ShowTitle();

            Console.WriteLine($"Category:   {category}");
            Console.WriteLine($"Difficulty: {difficulty}");
            Console.WriteLine($"Questions:  {questionCount}");
            Console.WriteLine();
            Console.WriteLine("Type Q at any question to end the quiz.");
            Console.WriteLine("Press ENTER to begin.");

            Input();

            QuizResult result = RunQuiz(category, difficulty, questionCount);

            ShowResults(result);
            SaveStatistics();

            Console.WriteLine();
            Console.WriteLine("Press ENTER to return to the main menu.");
            Input();
        }

        private static string AskForCategory()
        {
            Console.WriteLine("Choose a topic:");
            for (int i = 0; i < categoryNames.Count; i++)
            {
                Console.WriteLine($"  {i + 1}) {categoryNames[i]}");
            }
            Console.WriteLine($"  {categoryNames.Count + 1}) Mixed (all topics)");
            Console.WriteLine();

            int choice = ReadInt("Enter a number: ", 1, categoryNames.Count + 1);

            if (choice == categoryNames.Count + 1)
            {
                return "Mixed";
            }

            return categoryNames[choice - 1];
        }

        private static string AskForDifficulty()
        {
            Console.WriteLine();
            Console.WriteLine("Choose difficulty:");
            Console.WriteLine("  1) Easy");
            Console.WriteLine("  2) Medium");
            Console.WriteLine("  3) Hard");
            Console.WriteLine("  4) Mixed");
            Console.WriteLine();

            int choice = ReadInt("Enter a number: ", 1, 4);

            return choice switch
            {
                1 => "Easy",
                2 => "Medium",
                3 => "Hard",
                _ => "Mixed",
            };
        }

        private static int AskForQuestionCount()
        {
            Console.WriteLine();
            return ReadInt("How many questions? (1-50): ", 1, 50);
        }

        private static int ReadInt(string message, int minimum, int maximum)
        {
            while (true)
            {
                Console.Write(message);
                string userInput = Input();
                int? value = TryParseInt(userInput);
                if (value.HasValue && value.Value >= minimum && value.Value <= maximum)
                {
                    return value.Value;
                }
                Console.WriteLine($"Invalid input. Please enter a number from {minimum} to {maximum}.");
            }
        }

        // -------------------------------------------------------------------
        // Instructions
        // -------------------------------------------------------------------

        private static void ShowInstructions()
        {
            ClearScreen();
            ShowTitle();

            Console.WriteLine("HOW TO PLAY");
            Console.WriteLine("---------------------------------");
            Console.WriteLine("1. Choose a mathematics category.");
            Console.WriteLine("2. Select a difficulty level.");
            Console.WriteLine("3. Choose between 1 and 50 questions.");
            Console.WriteLine("4. Answer before the timer expires.");
            Console.WriteLine("5. Correct answers earn points.");
            Console.WriteLine("6. Consecutive correct answers build a streak.");
            Console.WriteLine("7. A hint can be used, but reduces the points for that question.");
            Console.WriteLine("8. Type Q to end a quiz early.");
            Console.WriteLine("9. Incorrect questions are shown at the end.");
            Console.WriteLine("10. Your overall statistics and high score are saved.");
            Console.WriteLine();
            Console.WriteLine("DIFFICULTY");
            Console.WriteLine("---------------------------------");
            Console.WriteLine("Easy   = introductory questions");
            Console.WriteLine("Medium = more calculations and multi-step questions");
            Console.WriteLine("Hard   = advanced questions and less time");
            Console.WriteLine();
            Console.WriteLine("Press ENTER to return to the main menu.");
            Input();
        }

        // -------------------------------------------------------------------
        // Quiz engine
        // -------------------------------------------------------------------

        private static QuizResult RunQuiz(string category, string difficulty, int questionCount)
        {
            int correctCount = 0;
            int totalPoints = 0;
            int currentStreak = 0;
            int bestStreak = 0;
            double totalTime = 0.0;
            int answeredQuestions = 0;
            var missedSummaries = new List<string>();

            for (int i = 0; i < questionCount; i++)
            {
                double progress = questionCount <= 1 ? 1.0 : (double)i / (questionCount - 1);
                Question question = GenerateQuestion(category, difficulty, progress);

                ClearScreen();
                ShowTitle();

                Console.WriteLine($"Category: {category}");
                Console.WriteLine($"Difficulty: {difficulty}");
                Console.WriteLine($"Question {i + 1} of {questionCount}");
                Console.WriteLine($"Current streak: {currentStreak}");
                Console.WriteLine($"Best streak this quiz: {bestStreak}");
                Console.WriteLine($"Points: {totalPoints}");
                Console.WriteLine();
                Console.WriteLine($"[{question.TimeLimitSeconds}s allowed]");
                Console.WriteLine(question.Prompt);
                Console.WriteLine();
                Console.WriteLine("Type H for a hint or Q to quit.");
                Console.Write("Answer: ");

                var stopwatch = Stopwatch.StartNew();
                string rawAnswer = Input();
                double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;

                if (rawAnswer.Trim().ToLowerInvariant() == "q")
                {
                    Console.WriteLine();
                    Console.WriteLine("Quiz ended early.");
                    break;
                }

                bool usedHint = false;

                if (rawAnswer.Trim().ToLowerInvariant() == "h")
                {
                    usedHint = true;
                    Console.WriteLine();
                    Console.WriteLine($"Hint: {question.Hint}");
                    Console.Write("Answer: ");

                    stopwatch.Restart();
                    rawAnswer = Input();
                    elapsedSeconds += stopwatch.Elapsed.TotalSeconds;

                    if (rawAnswer.Trim().ToLowerInvariant() == "q")
                    {
                        Console.WriteLine();
                        Console.WriteLine("Quiz ended early.");
                        break;
                    }
                }

                answeredQuestions++;
                totalTime += elapsedSeconds;

                bool withinTime = elapsedSeconds <= question.TimeLimitSeconds;
                double? userAnswer = TryParseDouble(rawAnswer);
                bool parsed = userAnswer.HasValue;

                bool isCorrect = withinTime
                    && parsed
                    && Math.Abs(userAnswer!.Value - question.Answer) <= question.Tolerance;

                if (isCorrect)
                {
                    correctCount++;
                    currentStreak++;

                    if (currentStreak > bestStreak)
                    {
                        bestStreak = currentStreak;
                    }

                    int pointsEarned = question.Points;

                    if (usedHint)
                    {
                        pointsEarned = Math.Max(1, pointsEarned / 2);
                    }

                    // Streak bonuses.
                    if (currentStreak >= 5)
                    {
                        pointsEarned += 20;
                    }
                    else if (currentStreak >= 3)
                    {
                        pointsEarned += 10;
                    }

                    totalPoints += pointsEarned;

                    Console.WriteLine();
                    Console.WriteLine($"Correct! +{pointsEarned} points");

                    if (currentStreak >= 3)
                    {
                        Console.WriteLine($"\U0001F525 {currentStreak} question streak!");
                    }
                }
                else
                {
                    currentStreak = 0;
                    Console.WriteLine();

                    if (!withinTime)
                    {
                        Console.WriteLine("Too slow!");
                        missedSummaries.Add(BuildReviewSummary(question, rawAnswer, "Ran out of time"));
                    }
                    else if (!parsed)
                    {
                        Console.WriteLine("That was not a valid number.");
                        missedSummaries.Add(BuildReviewSummary(question, rawAnswer, "Invalid answer"));
                    }
                    else
                    {
                        Console.WriteLine("Not quite.");
                        missedSummaries.Add(BuildReviewSummary(question, rawAnswer, "Incorrect"));
                    }

                    Console.WriteLine($"Correct answer: {FormatAnswer(question.Answer)}");
                    Console.WriteLine($"Explanation: {question.Explanation}");
                }

                UpdateCategoryStatistics(category, isCorrect);
                totalCorrect += isCorrect ? 1 : 0;
                totalQuestions++;

                Console.WriteLine();
                Console.WriteLine("Press ENTER to continue.");
                Input();
            }

            gamesPlayed++;

            double averageTime = answeredQuestions > 0 ? totalTime / answeredQuestions : 0.0;

            return new QuizResult
            {
                Correct = correctCount,
                Questions = answeredQuestions,
                Points = totalPoints,
                BestStreak = bestStreak,
                AverageTime = averageTime,
                Category = category,
                Difficulty = difficulty,
            };
        }

        private static string BuildReviewSummary(Question question, string userAnswer, string result)
        {
            string displayAnswer = userAnswer.Trim().Length > 0 ? userAnswer.Trim() : "(no answer)";
            return $"{question.Prompt}\n"
                + $"   Your answer: {displayAnswer}\n"
                + $"   Correct answer: {FormatAnswer(question.Answer)}\n"
                + $"   Result: {result}\n"
                + $"   Explanation: {question.Explanation}";
        }

        private static void ShowResults(QuizResult result)
        {
            ClearScreen();
            ShowTitle();

            double percentage = result.Questions > 0 ? (double)result.Correct / result.Questions * 100 : 0;

            Console.WriteLine("========== QUIZ RESULTS ==========");
            Console.WriteLine();
            Console.WriteLine($"Category:       {result.Category}");
            Console.WriteLine($"Difficulty:     {result.Difficulty}");
            Console.WriteLine($"Correct:        {result.Correct}/{result.Questions}");
            Console.WriteLine($"Percentage:     {Fixed(percentage, 0)}%");
            Console.WriteLine($"Points:         {result.Points}");
            Console.WriteLine($"Best streak:    {result.BestStreak}");
            Console.WriteLine($"Average time:   {Fixed(result.AverageTime, 1)} seconds");
            Console.WriteLine();

            if (result.Points > highScore)
            {
                highScore = result.Points;
                Console.WriteLine("NEW HIGH SCORE!");
            }
            else
            {
                Console.WriteLine($"High score:     {highScore}");
            }

            Console.WriteLine();
            Console.WriteLine("==================================");
        }

        // -------------------------------------------------------------------
        // Statistics
        // -------------------------------------------------------------------

        private static void UpdateCategoryStatistics(string category, bool correct)
        {
            if (category == "Mixed")
            {
                return;
            }

            if (!Statistics.TryGetValue(category, out CategoryStats? stats))
            {
                stats = new CategoryStats();
                Statistics[category] = stats;
            }

            stats.Attempted++;

            if (correct)
            {
                stats.Correct++;
            }
        }

        private static void ShowStatistics()
        {
            ClearScreen();
            ShowTitle();

            Console.WriteLine("========== STATISTICS ==========");
            Console.WriteLine();
            Console.WriteLine($"Games played:   {gamesPlayed}");
            Console.WriteLine($"Total questions: {totalQuestions}");
            Console.WriteLine($"Total correct:   {totalCorrect}");
            Console.WriteLine($"High score:      {highScore}");

            double overallPercentage = totalQuestions > 0 ? (double)totalCorrect / totalQuestions * 100 : 0;

            Console.WriteLine($"Overall accuracy: {Fixed(overallPercentage, 1),6}%");
            Console.WriteLine();

            Console.WriteLine("CATEGORY STATISTICS");
            Console.WriteLine("---------------------------------");

            foreach (string category in categoryNames)
            {
                CategoryStats stats = Statistics.TryGetValue(category, out CategoryStats? s) ? s : new CategoryStats();
                double percentage = stats.Attempted > 0 ? (double)stats.Correct / stats.Attempted * 100 : 0;
                Console.WriteLine($"{category,-22} {stats.Correct,3}/{stats.Attempted,-3} ({Fixed(percentage, 1)}%)");
            }

            Console.WriteLine();
            Console.WriteLine("Press ENTER to return to the main menu.");
            Input();
        }

        private static void LoadStatistics()
        {
            try
            {
                if (!File.Exists(StatisticsFile))
                {
                    return;
                }

                string[] lines = File.ReadAllLines(StatisticsFile, Encoding.UTF8);

                foreach (string line in lines)
                {
                    string[] parts = line.Trim().Split('|');

                    if (parts.Length == 2)
                    {
                        string key = parts[0];
                        string value = parts[1];
                        switch (key)
                        {
                            case "HighScore": highScore = TryParseInt(value) ?? 0; break;
                            case "TotalCorrect": totalCorrect = TryParseInt(value) ?? 0; break;
                            case "TotalQuestions": totalQuestions = TryParseInt(value) ?? 0; break;
                            case "GamesPlayed": gamesPlayed = TryParseInt(value) ?? 0; break;
                        }
                    }
                    else if (parts.Length == 4 && parts[0] == "Category")
                    {
                        Statistics[parts[1]] = new CategoryStats
                        {
                            Correct = TryParseInt(parts[2]) ?? 0,
                            Attempted = TryParseInt(parts[3]) ?? 0,
                        };
                    }
                }
            }
            catch (Exception)
            {
                Statistics.Clear();
            }
        }

        private static void SaveStatistics()
        {
            try
            {
                var lines = new List<string>
                {
                    $"HighScore|{highScore}",
                    $"TotalCorrect|{totalCorrect}",
                    $"TotalQuestions|{totalQuestions}",
                    $"GamesPlayed|{gamesPlayed}",
                };

                foreach (KeyValuePair<string, CategoryStats> entry in Statistics)
                {
                    lines.Add($"Category|{entry.Key}|{entry.Value.Correct}|{entry.Value.Attempted}");
                }

                File.WriteAllText(StatisticsFile, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
            }
            catch (Exception)
            {
                // Ignore save errors.
            }
        }

        // -------------------------------------------------------------------
        // Question selection
        // -------------------------------------------------------------------

        private static Question GenerateQuestion(string category, string difficulty, double progress)
        {
            List<Func<Question>> pool;
            if (category == "Mixed")
            {
                string selectedCategory = categoryNames[Randomizer.Next(categoryNames.Count)];
                pool = categories[selectedCategory];
            }
            else
            {
                pool = categories[category];
            }

            List<Func<Question>> filteredPool = FilterByDifficulty(pool, difficulty);

            if (filteredPool.Count == 0)
            {
                filteredPool = pool;
            }

            int targetIndex = (int)Math.Round(progress * (filteredPool.Count - 1));

            // Select from a small area around the target so the quiz
            // becomes gradually harder without becoming predictable.
            int windowStart = Math.Max(0, targetIndex - 1);
            int windowEnd = Math.Min(filteredPool.Count - 1, targetIndex + 1);

            int chosenIndex = RandInt(windowStart, windowEnd);

            return filteredPool[chosenIndex]();
        }

        private static List<Func<Question>> FilterByDifficulty(List<Func<Question>> pool, string difficulty)
        {
            if (difficulty == "Mixed")
            {
                return new List<Func<Question>>(pool);
            }

            int count = pool.Count;

            if (difficulty == "Easy")
            {
                return pool.Take(Math.Max(1, count / 3)).ToList();
            }

            if (difficulty == "Medium")
            {
                int start = count / 3;
                int length = Math.Max(1, count / 3);
                return pool.Skip(start).Take(length).ToList();
            }

            // Hard
            return pool.Skip(Math.Max(0, count * 2 / 3)).ToList();
        }

        // -------------------------------------------------------------------
        // Category configuration
        // -------------------------------------------------------------------

        /// <summary>
        /// Builds the full topic list, each topic ordered roughly from easiest to hardest.
        /// Order matters: difficulty filtering splits each pool into thirds by position.
        /// </summary>
        private static void BuildCategories()
        {
            var topics = new (string Name, Func<Question>[] Pool)[]
            {
                ("Arithmetic", new Func<Question>[]
                {
                    () => WholeAddition(1, 20),
                    () => WholeSubtraction(1, 20),
                    () => WholeMultiplication(2, 10),
                    () => WholeDivision(2, 10),
                    DecimalAddition,
                    DecimalSubtraction,
                    FractionOfNumber,
                    PercentageOf,
                    DecimalMultiplication,
                    PercentageChange,
                    OrderOfOperations,
                    FractionAddition,
                    RatioQuestion,
                    SquareRootQuestion,
                }),
                ("Number Theory", new Func<Question>[]
                {
                    ModuloBasic,
                    GcdTwoNumbers,
                    LcmTwoNumbers,
                    DivisorCount,
                    ModularExponentiation,
                    EulerTotient,
                }),
                ("Algebra", new Func<Question>[]
                {
                    SolveAdditionOneStep,
                    SolveMultiplicationOneStep,
                    SolveTwoStepLinear,
                    EvaluateExpression,
                    QuadraticLargerRoot,
                    LinearSystemSolveX,
                    ExpandBrackets,
                    FactoriseQuadratic,
                    SolveQuadratic,
                }),
                ("Precalculus", new Func<Question>[]
                {
                    FunctionCompositionLinear,
                    LogarithmBasic,
                    ExponentialEvaluate,
                    LogEquationSolve,
                    InverseFunctionLinear,
                    NaturalLogEvaluate,
                }),
                ("Geometry", new Func<Question>[]
                {
                    RectanglePerimeter,
                    RectangleArea,
                    TriangleArea,
                    CircleArea,
                    PythagoreanHypotenuse,
                    RectangularPrismVolume,
                    CircleCircumference,
                    TriangleMissingSide,
                    SurfaceAreaCuboid,
                }),
                ("Trigonometry", new Func<Question>[]
                {
                    SineOfCommonAngle,
                    CosineOfCommonAngle,
                    TangentOfCommonAngle,
                    TriangleMissingAngle,
                    RightTriangleOppositeSide,
                    RightTriangleAdjacentSide,
                }),
                ("Combinatorics", new Func<Question>[]
                {
                    FactorialValue,
                    PermutationsCount,
                    CombinationsCount,
                    CoinFlipProbability,
                }),
                ("Statistics", new Func<Question>[]
                {
                    MeanOfList,
                    RangeOfList,
                    MedianOfList,
                    SimpleProbability,
                    ModeOfList,
                    ProbabilityAsPercentage,
                }),
                ("Complex Numbers", new Func<Question>[]
                {
                    ComplexAdditionComponent,
                    ComplexModulus,
                    ComplexMultiplicationComponent,
                    ComplexConjugateModulusSquared,
                    ComplexPowerDeMoivre,
                }),
                ("Sequences & Series", new Func<Question>[]
                {
                    ArithmeticSequenceNthTerm,
                    GeometricSequenceNthTerm,
                    ArithmeticSeriesSum,
                    GeometricSeriesSum,
                    InfiniteGeometricSeriesSum,
                }),
                ("Calculus", new Func<Question>[]
                {
                    DerivativeOfQuadraticAtPoint,
                    DerivativeOfCubicAtPoint,
                    DefiniteIntegralOfLinear,
                    LimitAtInfinityRatio,
                    LimitByFactoring,
                }),
                ("Multivariable Calculus", new Func<Question>[]
                {
                    PartialDerivativeXAtPoint,
                    PartialDerivativeYAtPoint,
                    GradientMagnitudeAtPoint,
                    DoubleIntegralOverRectangle,
                    DivergenceAtPoint,
                }),
                ("Differential Equations", new Func<Question>[]
                {
                    ExponentialGrowthAtTime,
                    ExponentialDecayHalfLife,
                    NewtonsLawOfCooling,
                    CharacteristicEquationLargerRoot,
                    LogisticGrowthAtTime,
                }),
                ("Linear Algebra", new Func<Question>[]
                {
                    VectorAdditionComponent,
                    VectorDotProduct2D,
                    VectorMagnitude2D,
                    MatrixDeterminant2x2,
                    MatrixAdditionEntry,
                }),
            };

            categoryNames = topics.Select(t => t.Name).ToList();
            categories = topics.ToDictionary(t => t.Name, t => t.Pool.ToList());
        }

        // -------------------------------------------------------------------
        // Arithmetic
        // -------------------------------------------------------------------

        private static Question WholeAddition(int minValue, int maxValue)
        {
            int a = RandInt(minValue, maxValue);
            int b = RandInt(minValue, maxValue);
            return MakeQuestion($"{a} + {b}", a + b, 12,
                "Add the two numbers.",
                $"Add {a} and {b}.");
        }

        private static Question WholeSubtraction(int minValue, int maxValue)
        {
            int a = RandInt(minValue, maxValue);
            int b = RandInt(minValue, a);
            return MakeQuestion($"{a} - {b}", a - b, 12,
                "Subtract the smaller number from the larger number.",
                $"Calculate {a} minus {b}.");
        }

        private static Question WholeMultiplication(int minValue, int maxValue)
        {
            int a = RandInt(minValue, maxValue);
            int b = RandInt(minValue, maxValue);
            return MakeQuestion($"{a} x {b}", a * b, 15,
                "Use multiplication.",
                $"Calculate {a} groups of {b}.");
        }

        private static Question WholeDivision(int minValue, int maxValue)
        {
            int divisor = RandInt(minValue, maxValue);
            int quotient = RandInt(minValue, maxValue);
            int dividend = divisor * quotient;
            return MakeQuestion($"{dividend} / {divisor}", quotient, 15,
                "Think about the multiplication fact that gives the dividend.",
                $"{dividend} divided by {divisor} equals {quotient}.");
        }

        private static Question DecimalAddition()
        {
            double a = RandInt(10, 499) / 10.0;
            double b = RandInt(10, 499) / 10.0;
            return MakeQuestion($"{a:F1} + {b:F1}", RoundHalfEven(a + b, 1), 18,
                "Line up the decimal places.",
                "Add the numbers while keeping the decimal place aligned.");
        }

        private static Question DecimalSubtraction()
        {
            double a = RandInt(50, 499) / 10.0;
            double b = RandInt(0, (int)(a * 10) - 1) / 10.0;
            return MakeQuestion($"{a:F1} - {b:F1}", RoundHalfEven(a - b, 1), 18,
                "Line up the decimal places.",
                "Subtract the second decimal from the first.");
        }

        private static Question DecimalMultiplication()
        {
            double a = RandInt(10, 99) / 10.0;
            int b = RandInt(2, 9);
            return MakeQuestion($"{a:F1} x {b}", RoundHalfEven(a * b, 2), 22,
                "Multiply first, then place the decimal.",
                "Multiply the decimal by the whole number.");
        }

        private static Question FractionOfNumber()
        {
            int denominator = Choice(new[] { 2, 3, 4, 5, 10 });
            int numerator = RandInt(1, denominator - 1);
            int multiplier = RandInt(2, 11);
            int whole = denominator * multiplier;

            double answer = (double)numerator / denominator * whole;
            return MakeQuestion($"{numerator}/{denominator} of {whole}", answer, 20,
                "Divide by the denominator, then multiply by the numerator.",
                "A fraction of a number can be found using fraction x whole number.");
        }

        private static Question PercentageOf()
        {
            int percent = Choice(new[] { 5, 10, 15, 20, 25, 50, 75 });
            int baseNumber = RandInt(2, 40) * 4;

            double answer = RoundHalfEven(baseNumber * percent / 100.0, 2);
            return MakeQuestion($"{percent}% of {baseNumber}", answer, 20,
                "Convert the percentage to a decimal.",
                $"{percent}% means {percent}/100.");
        }

        private static Question PercentageChange()
        {
            int percent = Choice(new[] { 10, 20, 25, 50 });
            int baseNumber = RandInt(10, 199);
            bool isIncrease = Randomizer.Next(2) == 0;

            double answer = isIncrease
                ? RoundHalfEven(baseNumber * (1 + percent / 100.0), 2)
                : RoundHalfEven(baseNumber * (1 - percent / 100.0), 2);

            string action = isIncrease ? "increased by" : "decreased by";
            return MakeQuestion($"{baseNumber} {action} {percent}%", answer, 24,
                "For an increase multiply by 1 + percentage. For a decrease multiply by 1 - percentage.",
                $"Apply a {percent}% change to {baseNumber}.");
        }

        private static Question OrderOfOperations()
        {
            int a = RandInt(2, 9);
            int b = RandInt(2, 9);
            int c = RandInt(1, 9);
            int answer = a + b * c;
            return MakeQuestion($"{a} + {b} x {c}", answer, 20,
                "Remember BIDMAS/BODMAS: multiplication comes before addition.",
                "Multiply first, then add.");
        }

        private static Question FractionAddition()
        {
            int denominator = RandInt(2, 7);
            int numerator1 = RandInt(1, denominator - 1);
            int numerator2 = RandInt(1, denominator - 1);
            double answer = (double)(numerator1 + numerator2) / denominator;
            return MakeQuestion($"{numerator1}/{denominator} + {numerator2}/{denominator}", answer, 20,
                "The denominators are already the same.",
                "Add the numerators and keep the denominator.");
        }

        private static Question RatioQuestion()
        {
            int ratioA = RandInt(1, 5);
            int ratioB = RandInt(1, 5);
            int multiplier = RandInt(2, 9);
            int firstAmount = ratioA * multiplier;
            return MakeQuestion(
                $"A ratio is {ratioA}:{ratioB}. If the first amount is {firstAmount}, what is the second amount?",
                ratioB * multiplier, 25,
                $"Find the multiplier: {firstAmount} / {ratioA}.",
                $"The ratio is multiplied by {multiplier}.");
        }

        private static Question SquareRootQuestion()
        {
            int root = RandInt(2, 12);
            int value = root * root;
            return MakeQuestion($"\u221a{value}", root, 18,
                "Think of a number multiplied by itself.",
                $"{root} x {root} = {value}.");
        }

        // -------------------------------------------------------------------
        // Number Theory
        // -------------------------------------------------------------------

        private static Question ModuloBasic()
        {
            int a = RandInt(10, 99);
            int b = RandInt(2, 9);
            return MakeQuestion($"{a} mod {b}", a % b, 15,
                "Find the remainder after division.",
                $"{a} divided by {b} leaves remainder {a % b}.");
        }

        private static Question GcdTwoNumbers()
        {
            int a = RandInt(4, 60);
            int b = RandInt(4, 60);
            return MakeQuestion($"GCD of {a} and {b}", Gcd(a, b), 20,
                "Find the largest number that divides both.",
                $"The greatest common divisor is {Gcd(a, b)}.");
        }

        private static Question LcmTwoNumbers()
        {
            int a = RandInt(2, 20);
            int b = RandInt(2, 20);
            int answer = a * b / Gcd(a, b);
            return MakeQuestion($"LCM of {a} and {b}", answer, 22,
                "Use the formula: LCM = a * b / GCD(a, b).",
                $"The least common multiple is {answer}.");
        }

        private static Question DivisorCount()
        {
            int n = RandInt(10, 100);
            int count = Enumerable.Range(1, n).Count(d => n % d == 0);
            return MakeQuestion($"How many positive divisors does {n} have?", count, 28,
                "Check every number from 1 to n that divides evenly.",
                $"{n} has {count} positive divisors.");
        }

        private static Question ModularExponentiation()
        {
            int baseValue = RandInt(2, 9);
            int exponent = RandInt(2, 6);
            int modulus = RandInt(3, 13);
            int answer = ModPow(baseValue, exponent, modulus);
            return MakeQuestion($"{baseValue}^{exponent} mod {modulus}", answer, 30,
                "Compute the power first, then take the remainder.",
                $"{baseValue}^{exponent} mod {modulus} = {answer}.");
        }

        private static Question EulerTotient()
        {
            int n = RandInt(2, 40);
            int count = Enumerable.Range(1, n).Count(k => Gcd(k, n) == 1);
            return MakeQuestion(
                $"Euler's totient phi({n}) -- how many integers from 1 to {n} are coprime with {n}?",
                count, 35,
                "Count numbers sharing no common factor with n.",
                $"phi({n}) = {count}.");
        }

        // -------------------------------------------------------------------
        // Algebra
        // -------------------------------------------------------------------

        private static Question SolveAdditionOneStep()
        {
            int x0 = RandInt(1, 29);
            int a = RandInt(1, 29);
            int b = x0 + a;
            return MakeQuestion($"Solve for x: x + {a} = {b}", x0, 15,
                $"Subtract {a} from both sides.",
                $"x = {b} - {a}.");
        }

        private static Question SolveMultiplicationOneStep()
        {
            int a = RandInt(2, 11);
            int x0 = RandInt(1, 11);
            int b = a * x0;
            return MakeQuestion($"Solve for x: {a}x = {b}", x0, 15,
                $"Divide both sides by {a}.",
                $"x = {b} / {a}.");
        }

        private static Question SolveTwoStepLinear()
        {
            int a = RandInt(2, 8);
            int x0 = RandInt(1, 11);
            int b = RandInt(1, 19);
            int c = a * x0 + b;
            return MakeQuestion($"Solve for x: {a}x + {b} = {c}", x0, 20,
                $"Subtract {b}, then divide by {a}.",
                $"x = ({c} - {b}) / {a}.");
        }

        private static Question EvaluateExpression()
        {
            int a = RandInt(1, 5);
            int b = RandInt(-6, 6);
            int c = RandInt(-6, 6);
            int x0 = RandInt(-4, 4);
            int answer = a * x0 * x0 + b * x0 + c;
            return MakeQuestion($"If x = {x0}, evaluate {a}x^2 + ({b})x + ({c})", answer, 20,
                $"Substitute x = {x0} into the expression.",
                "Replace every x with the given value, then calculate.");
        }

        private static Question QuadraticLargerRoot()
        {
            int p = RandInt(-10, 10);
            int q = p;
            while (q == p)
            {
                q = RandInt(-10, 10);
            }

            int answer = Math.Max(p, q);
            return MakeQuestion($"(x - ({p}))(x - ({q})) = 0. What is the larger solution for x?", answer, 25,
                "Set each bracket equal to zero.",
                $"The two solutions are x = {p} and x = {q}.");
        }

        private static Question LinearSystemSolveX()
        {
            int x0 = RandInt(1, 9);
            int y0 = RandInt(1, 9);

            int determinant = 0;
            int a1 = 0, b1 = 0, a2 = 0, b2 = 0;
            while (determinant == 0)
            {
                a1 = RandInt(1, 5);
                b1 = RandInt(1, 5);
                a2 = RandInt(1, 5);
                b2 = RandInt(1, 5);
                determinant = a1 * b2 - a2 * b1;
            }

            int c1 = a1 * x0 + b1 * y0;
            int c2 = a2 * x0 + b2 * y0;

            return MakeQuestion($"{a1}x + {b1}y = {c1}; {a2}x + {b2}y = {c2}. Find x.", x0, 30,
                "Use elimination to remove y.",
                $"The generated solution has x = {x0}.");
        }

        private static Question ExpandBrackets()
        {
            int a = RandInt(2, 7);
            int b = RandInt(1, 7);
            int answer = a * b;
            return MakeQuestion($"Find the constant term when expanding (x + {a})(x + {b})", answer, 25,
                "Multiply the two constants.",
                $"{a} x {b} = {answer}.");
        }

        private static Question FactoriseQuadratic()
        {
            int a = RandInt(1, 7);
            int b = RandInt(1, 7);
            int answer = a + b;
            return MakeQuestion($"For x^2 + {a + b}x + {a * b}, enter the sum of the two factor values.", answer, 25,
                $"The factors multiply to {a * b}.",
                $"The factor values are {a} and {b}, whose sum is {a + b}.");
        }

        private static Question SolveQuadratic()
        {
            int root = RandInt(-6, 6);
            int constant = root * root;
            return MakeQuestion($"Solve x^2 = {constant}. Enter the positive solution.", Math.Abs(root), 22,
                "Take the square root of both sides.",
                $"The positive square root of {constant} is {Math.Abs(root)}.");
        }

        // -------------------------------------------------------------------
        // Precalculus
        // -------------------------------------------------------------------

        private static Question FunctionCompositionLinear()
        {
            int a = RandInt(1, 5);
            int b = RandInt(-5, 5);
            int c = RandInt(1, 5);
            int d = RandInt(-5, 5);
            int x0 = RandInt(-4, 4);
            int inner = c * x0 + d;
            int answer = a * inner + b;
            return MakeQuestion($"f(x) = {a}x + ({b}), g(x) = {c}x + ({d}). Find f(g({x0}))", answer, 20,
                "Evaluate g(x0) first, then substitute into f.",
                $"g({x0}) = {inner}, then f({inner}) = {answer}.");
        }

        private static Question LogarithmBasic()
        {
            int baseValue = Choice(new[] { 2, 3, 5, 10 });
            int exponent = RandInt(1, 5);
            int value = IntPow(baseValue, exponent);
            return MakeQuestion($"log base {baseValue} of {value}", exponent, 20,
                $"Ask: {baseValue} to what power equals {value}?",
                $"{baseValue}^{exponent} = {value}, so log = {exponent}.");
        }

        private static Question ExponentialEvaluate()
        {
            int baseValue = RandInt(2, 5);
            int exponent = RandInt(-3, 4);
            double answer = Math.Pow(baseValue, exponent);
            string answerText = exponent >= 0 ? IntPow(baseValue, exponent).ToString() : PyFloat(answer);
            return MakeQuestion($"{baseValue}^({exponent})", answer, 22,
                "Multiply the base by itself the given number of times.",
                $"{baseValue}^{exponent} = {answerText}.");
        }

        private static Question LogEquationSolve()
        {
            int baseValue = Choice(new[] { 2, 3, 5 });
            int x0 = RandInt(1, 6);
            int value = IntPow(baseValue, x0);
            return MakeQuestion($"Solve for x: {baseValue}^x = {value}", x0, 25,
                $"Express {value} as a power of {baseValue}.",
                $"{baseValue}^{x0} = {value}, so x = {x0}.");
        }

        private static Question InverseFunctionLinear()
        {
            int a = RandInt(2, 9);
            int b = RandInt(-9, 9);
            int x0 = RandInt(-10, 10);
            int y0 = a * x0 + b;
            return MakeQuestion($"f(x) = {a}x + ({b}). Find f^-1({y0}) -- i.e. the x that gives this y", x0, 28,
                "Swap x and y, then solve for x.",
                $"Solve {a}x + ({b}) = {y0}, giving x = {x0}.");
        }

        private static Question NaturalLogEvaluate()
        {
            int x0 = RandInt(1, 50);
            double answer = Math.Log(x0);
            return MakeQuestion($"ln({x0})", answer, 25,
                "Use the natural logarithm (base e).",
                $"ln({x0}) \u2248 {Fixed(answer, 4)}.");
        }

        // -------------------------------------------------------------------
        // Geometry
        // -------------------------------------------------------------------

        private static Question RectanglePerimeter()
        {
            int length = RandInt(2, 29);
            int width = RandInt(2, 29);
            int answer = 2 * (length + width);
            return MakeQuestion($"Perimeter of a rectangle with length {length} and width {width}", answer, 15,
                "Use P = 2(l + w).",
                $"2 x ({length} + {width}) = {answer}.");
        }

        private static Question RectangleArea()
        {
            int length = RandInt(2, 29);
            int width = RandInt(2, 29);
            int answer = length * width;
            return MakeQuestion($"Area of a rectangle with length {length} and width {width}", answer, 15,
                "Use A = length x width.",
                $"{length} x {width} = {answer}.");
        }

        private static Question TriangleArea()
        {
            int baseLength = RandInt(2, 29);
            int height = RandInt(2, 29);
            double answer = 0.5 * baseLength * height;
            return MakeQuestion($"Area of a triangle with base {baseLength} and height {height}", answer, 18,
                "Use A = 1/2 x base x height.",
                $"Half of {baseLength} x {height} gives the area.");
        }

        private static Question CircleArea()
        {
            int radius = RandInt(2, 19);
            double answer = 3.14159 * radius * radius;
            return MakeQuestion($"Area of a circle with radius {radius} (use pi = 3.14159)", answer, 22,
                "Use A = pi x r^2.",
                $"3.14159 x {radius} x {radius} = {Fixed(answer, 2)}.");
        }

        private static Question PythagoreanHypotenuse()
        {
            int legA = RandInt(3, 19);
            int legB = RandInt(3, 19);
            double answer = Math.Sqrt(legA * legA + legB * legB);
            return MakeQuestion($"Hypotenuse of a right triangle with legs {legA} and {legB}", answer, 20,
                "Use a^2 + b^2 = c^2.",
                $"Take the square root of {legA}^2 + {legB}^2.");
        }

        private static Question RectangularPrismVolume()
        {
            int length = RandInt(2, 14);
            int width = RandInt(2, 14);
            int height = RandInt(2, 14);
            int answer = length * width * height;
            return MakeQuestion(
                $"Volume of a rectangular prism with length {length}, width {width}, height {height}", answer, 20,
                "Use V = length x width x height.",
                $"Multiply {length} x {width} x {height}.");
        }

        private static Question CircleCircumference()
        {
            int radius = RandInt(2, 14);
            double answer = 2 * 3.14159 * radius;
            return MakeQuestion($"Circumference of a circle with radius {radius} (use pi = 3.14159)", answer, 22,
                "Use C = 2 x pi x r.",
                $"2 x 3.14159 x {radius} = {Fixed(answer, 2)}.");
        }

        private static Question TriangleMissingSide()
        {
            int a = RandInt(3, 11);
            int b = RandInt(3, 11);
            double c = Math.Sqrt(a * a + b * b);
            return MakeQuestion($"A right triangle has legs {a} and {b}. Find the hypotenuse.", c, 22,
                "Use Pythagoras: c = sqrt(a^2 + b^2).",
                $"sqrt({a}^2 + {b}^2) = {Fixed(c, 2)}.");
        }

        private static Question SurfaceAreaCuboid()
        {
            int l = RandInt(2, 9);
            int w = RandInt(2, 9);
            int h = RandInt(2, 9);
            int answer = 2 * (l * w + l * h + w * h);
            return MakeQuestion($"Surface area of a cuboid {l} x {w} x {h}", answer, 28,
                "Use SA = 2(lw + lh + wh).",
                $"2 x ({l}x{w} + {l}x{h} + {w}x{h}).");
        }

        // -------------------------------------------------------------------
        // Trigonometry
        // -------------------------------------------------------------------

        private static readonly int[] CommonAngles = { 0, 30, 45, 60, 90 };

        private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

        private static Question SineOfCommonAngle()
        {
            int angle = Choice(CommonAngles);
            double answer = Math.Sin(DegreesToRadians(angle));
            return MakeQuestion($"sin({angle} degrees)", answer, 15,
                "Use a standard sine value.",
                $"sin({angle}) \u2248 {Fixed(answer, 3)}.");
        }

        private static Question CosineOfCommonAngle()
        {
            int angle = Choice(CommonAngles);
            double answer = Math.Cos(DegreesToRadians(angle));
            return MakeQuestion($"cos({angle} degrees)", answer, 15,
                "Use a standard cosine value.",
                $"cos({angle}) \u2248 {Fixed(answer, 3)}.");
        }

        private static Question TangentOfCommonAngle()
        {
            int angle = Choice(new[] { 0, 30, 45, 60 }); // 90 is undefined, excluded
            double answer = Math.Tan(DegreesToRadians(angle));
            return MakeQuestion($"tan({angle} degrees)", answer, 15,
                "Remember tan = opposite / adjacent.",
                $"tan({angle}) \u2248 {Fixed(answer, 3)}.");
        }

        private static Question TriangleMissingAngle()
        {
            int angleA = RandInt(20, 99);
            int angleB = RandInt(20, 149 - angleA);
            int angleC = 180 - angleA - angleB;
            return MakeQuestion($"A triangle has angles {angleA} and {angleB} degrees. Find the third angle.", angleC, 15,
                "Angles in a triangle add to 180 degrees.",
                $"{angleC} = 180 - {angleA} - {angleB}.");
        }

        private static Question RightTriangleOppositeSide()
        {
            int angle = Choice(new[] { 30, 45, 60 });
            int hypotenuse = RandInt(5, 29);
            double answer = hypotenuse * Math.Sin(DegreesToRadians(angle));
            return MakeQuestion(
                $"Right triangle with hypotenuse {hypotenuse} and angle {angle} degrees. Find the opposite side.",
                answer, 25,
                "Use opposite = hypotenuse x sin(angle).",
                $"Multiply {hypotenuse} by sin({angle}).");
        }

        private static Question RightTriangleAdjacentSide()
        {
            int angle = Choice(new[] { 30, 45, 60 });
            int hypotenuse = RandInt(5, 29);
            double answer = hypotenuse * Math.Cos(DegreesToRadians(angle));
            return MakeQuestion(
                $"Right triangle with hypotenuse {hypotenuse} and angle {angle} degrees. Find the adjacent side.",
                answer, 25,
                "Use adjacent = hypotenuse x cos(angle).",
                $"Multiply {hypotenuse} by cos({angle}).");
        }

        // -------------------------------------------------------------------
        // Combinatorics
        // -------------------------------------------------------------------

        private static Question FactorialValue()
        {
            int n = RandInt(3, 8);
            long answer = Factorial(n);
            return MakeQuestion($"{n}! (factorial)", answer, 15,
                "Multiply all integers from 1 to n.",
                $"{n}! = {answer}.");
        }

        private static Question PermutationsCount()
        {
            int n = RandInt(4, 10);
            int r = RandInt(2, n);
            long answer = Permutations(n, r);
            return MakeQuestion(
                $"How many ways to arrange {r} items chosen from {n} distinct items (order matters)? P({n},{r})",
                answer, 24,
                "Use P(n, r) = n! / (n - r)!.",
                $"P({n},{r}) = {answer}.");
        }

        private static Question CombinationsCount()
        {
            int n = RandInt(4, 12);
            int r = RandInt(2, n);
            long answer = Combinations(n, r);
            return MakeQuestion(
                $"How many ways to choose {r} items from {n} distinct items (order doesn't matter)? C({n},{r})",
                answer, 24,
                "Use C(n, r) = n! / (r! * (n - r)!).",
                $"C({n},{r}) = {answer}.");
        }

        private static Question CoinFlipProbability()
        {
            int n = RandInt(3, 6);
            int k = RandInt(0, n);
            double answer = RoundHalfEven((double)Combinations(n, k) / IntPow(2, n), 4);
            return MakeQuestion($"Flipping a fair coin {n} times, what is P(exactly {k} heads)? (as a decimal)", answer, 32,
                "Use the binomial formula: C(n,k) / 2^n.",
                $"C({n},{k}) / 2^{n} = {PyFloat(answer)}.");
        }

        // -------------------------------------------------------------------
        // Calculus
        // -------------------------------------------------------------------

        private static Question DerivativeOfQuadraticAtPoint()
        {
            int a = RandInt(1, 5);
            int b = RandInt(-6, 6);
            int c = RandInt(-6, 6);
            int x0 = RandInt(-4, 4);
            int answer = 2 * a * x0 + b;
            return MakeQuestion($"f(x) = {a}x^2 + ({b})x + ({c}). Find f'({x0})", answer, 25,
                "Differentiate ax^2 to 2ax, and bx to b.",
                $"f'(x) = {2 * a}x + ({b}). Substitute x = {x0}.");
        }

        private static Question DerivativeOfCubicAtPoint()
        {
            int a = RandInt(1, 3);
            int b = RandInt(-4, 4);
            int c = RandInt(-4, 4);
            int x0 = RandInt(-3, 3);
            int answer = 3 * a * x0 * x0 + 2 * b * x0 + c;
            return MakeQuestion($"f(x) = {a}x^3 + ({b})x^2 + ({c})x. Find f'({x0})", answer, 30,
                "Use the power rule on each term.",
                $"Differentiate to {3 * a}x^2 + {2 * b}x + {c}.");
        }

        private static Question DefiniteIntegralOfLinear()
        {
            int a = RandInt(1, 5);
            int b = RandInt(-5, 5);
            int upperBound = RandInt(2, 7);
            double answer = a / 2.0 * upperBound * upperBound + b * upperBound;
            return MakeQuestion($"Evaluate the definite integral of ({a}x + {b}) dx from 0 to {upperBound}", answer, 30,
                "Find the antiderivative, then substitute the bounds.",
                $"The antiderivative is {PyFloat(a / 2.0)}x^2 + {b}x.");
        }

        private static Question LimitAtInfinityRatio()
        {
            int a = RandInt(1, 8);
            int b = RandInt(1, 8);
            double answer = (double)a / b;
            return MakeQuestion($"Find the limit as x approaches infinity of ({a}x + 7) / ({b}x - 3)", answer, 25,
                "Compare the coefficients of the highest powers of x.",
                $"The limit is {a}/{b}.");
        }

        private static Question LimitByFactoring()
        {
            int p = RandInt(2, 9);
            int answer = 2 * p;
            return MakeQuestion($"Find the limit as x approaches {p} of (x^2 - {p * p}) / (x - {p})", answer, 25,
                "Factor the numerator using difference of squares.",
                $"After cancelling, the expression becomes x + {p}.");
        }

        // -------------------------------------------------------------------
        // Multivariable Calculus
        // -------------------------------------------------------------------

        private static Question PartialDerivativeXAtPoint()
        {
            int a = RandInt(1, 5);
            int b = RandInt(-5, 5);
            int c = RandInt(-5, 5);
            int x0 = RandInt(-4, 4);
            int y0 = RandInt(-4, 4);
            // f(x, y) = a*x^2 + b*x*y + c*y^2  ->  df/dx = 2ax + by
            int answer = 2 * a * x0 + b * y0;
            return MakeQuestion(
                $"f(x,y) = {a}x^2 + ({b})xy + ({c})y^2. Find the partial derivative df/dx at ({x0}, {y0})", answer, 30,
                "Differentiate with respect to x, treating y as constant.",
                $"df/dx = {2 * a}x + ({b})y. At ({x0}, {y0}) = {answer}.");
        }

        private static Question PartialDerivativeYAtPoint()
        {
            int a = RandInt(1, 5);
            int b = RandInt(-5, 5);
            int c = RandInt(1, 5);
            int x0 = RandInt(-4, 4);
            int y0 = RandInt(-4, 4);
            // f(x, y) = a*x^2 + b*x*y + c*y^2  ->  df/dy = bx + 2cy
            int answer = b * x0 + 2 * c * y0;
            return MakeQuestion(
                $"f(x,y) = {a}x^2 + ({b})xy + ({c})y^2. Find the partial derivative df/dy at ({x0}, {y0})", answer, 30,
                "Differentiate with respect to y, treating x as constant.",
                $"df/dy = ({b})x + {2 * c}y. At ({x0}, {y0}) = {answer}.");
        }

        private static Question GradientMagnitudeAtPoint()
        {
            int a = RandInt(1, 4);
            int b = RandInt(1, 4);
            int x0 = RandInt(1, 6);
            int y0 = RandInt(1, 6);
            // f(x, y) = a*x^2 + b*y^2  ->  grad f = (2ax, 2by)
            int gx = 2 * a * x0;
            int gy = 2 * b * y0;
            double answer = Math.Sqrt(gx * gx + gy * gy);
            return MakeQuestion($"f(x,y) = {a}x^2 + {b}y^2. Find the magnitude of the gradient at ({x0}, {y0})", answer, 32,
                "Compute both partials, then take the Euclidean norm.",
                $"grad = ({gx}, {gy}), magnitude = {Fixed(answer, 2)}.");
        }

        private static Question DoubleIntegralOverRectangle()
        {
            int a = RandInt(1, 5);
            int b = RandInt(1, 5);
            int xMax = RandInt(2, 5);
            int yMax = RandInt(2, 5);
            // integral over [0, xMax] x [0, yMax] of (a*x + b*y) dA
            double answer = a * (xMax * xMax) / 2.0 * yMax + b * (yMax * yMax) / 2.0 * xMax;
            return MakeQuestion(
                $"Evaluate the double integral of ({a}x + {b}y) dA over the rectangle [0,{xMax}] x [0,{yMax}]", answer, 40,
                "Integrate with respect to x first, then y.",
                $"Result = {a}/2 * {xMax}^2 * {yMax} + {b}/2 * {yMax}^2 * {xMax}.");
        }

        private static Question DivergenceAtPoint()
        {
            int a = RandInt(1, 5);
            int b = RandInt(1, 5);
            int x0 = RandInt(-4, 4);
            int y0 = RandInt(-4, 4);
            // F(x, y) = (a*x^2, b*y^2)  ->  div F = 2ax + 2by
            int answer = 2 * a * x0 + 2 * b * y0;
            return MakeQuestion(
                $"Vector field F(x,y) = ({a}x^2, {b}y^2). Find the divergence of F at ({x0}, {y0})", answer, 35,
                "Divergence is the sum of partial derivatives of each component.",
                $"div F = {2 * a}x + {2 * b}y. At ({x0}, {y0}) = {answer}.");
        }

        // -------------------------------------------------------------------
        // Differential Equations
        // -------------------------------------------------------------------

        private static Question ExponentialGrowthAtTime()
        {
            int y0 = RandInt(10, 100);
            double k = RandInt(1, 5) / 10.0;
            int t = RandInt(1, 5);
            // dy/dt = k*y, y(0) = y0  ->  y(t) = y0 * e^(k*t)
            double answer = y0 * Math.Exp(k * t);
            return MakeQuestion($"dy/dt = {PyFloat(k)}y, y(0) = {y0}. Find y({t}) (exponential growth)", answer, 32,
                "Use the solution y(t) = y0 * e^(kt).",
                $"y({t}) = {y0} * e^({PyFloat(k)} * {t}) \u2248 {Fixed(answer, 2)}.");
        }

        private static Question ExponentialDecayHalfLife()
        {
            int initial = RandInt(100, 1000);
            int halfLife = RandInt(2, 10);
            int elapsed = halfLife * RandInt(1, 3);
            double answer = initial * Math.Pow(0.5, (double)elapsed / halfLife);
            return MakeQuestion(
                $"A substance with half-life {halfLife} years starts at {initial}g. How much remains after {elapsed} years?",
                answer, 32,
                "Use N(t) = N0 * (1/2)^(t / half_life).",
                $"{initial} * (1/2)^({elapsed}/{halfLife}) \u2248 {Fixed(answer, 2)}.");
        }

        private static Question NewtonsLawOfCooling()
        {
            int tEnv = RandInt(15, 25);
            int t0 = RandInt(70, 100);
            double k = RandInt(1, 3) / 10.0;
            int t = RandInt(1, 5);
            // T(t) = T_env + (T0 - T_env) * e^(-k*t)
            double answer = tEnv + (t0 - tEnv) * Math.Exp(-k * t);
            return MakeQuestion(
                $"Newton's law of cooling: room temperature {tEnv} degrees, object starts at {t0} degrees, "
                + $"cooling constant k = {PyFloat(k)}. Find the temperature at t = {t} (T(t) = T_env + (T0-T_env)e^(-kt))",
                answer, 36,
                "Apply T(t) = T_env + (T0 - T_env) * e^(-kt).",
                $"T({t}) = {tEnv} + ({t0} - {tEnv}) * e^(-{PyFloat(k)}*{t}) \u2248 {Fixed(answer, 2)}.");
        }

        private static Question CharacteristicEquationLargerRoot()
        {
            int p = RandInt(-8, 8);
            int q = p;
            while (q == p)
            {
                q = RandInt(-8, 8);
            }
            int b = -(p + q);
            int c = p * q;
            int answer = Math.Max(p, q);
            return MakeQuestion(
                $"For the ODE y'' + ({b})y' + ({c})y = 0, the characteristic equation has two real roots. Find the larger root.",
                answer, 32,
                "Solve r^2 + br + c = 0.",
                $"The roots are {p} and {q}; the larger is {answer}.");
        }

        private static Question LogisticGrowthAtTime()
        {
            int carryingCapacity = RandInt(500, 1000);
            int p0 = RandInt(10, 50);
            double r = RandInt(1, 3) / 10.0;
            int t = RandInt(1, 5);
            // P(t) = K / (1 + ((K - P0) / P0) * e^(-r*t))
            double answer = carryingCapacity / (1 + ((double)(carryingCapacity - p0) / p0) * Math.Exp(-r * t));
            return MakeQuestion(
                $"Logistic growth: carrying capacity {carryingCapacity}, P(0) = {p0}, growth rate r = {PyFloat(r)}. Find P({t}).",
                answer, 40,
                "Use P(t) = K / (1 + ((K - P0) / P0) * e^(-rt)).",
                $"P({t}) \u2248 {Fixed(answer, 2)}.");
        }

        // -------------------------------------------------------------------
        // Linear Algebra
        // -------------------------------------------------------------------

        private static Question VectorAdditionComponent()
        {
            int a1 = RandInt(-10, 10);
            int a2 = RandInt(-10, 10);
            int b1 = RandInt(-10, 10);
            int b2 = RandInt(-10, 10);
            return MakeQuestion($"u = ({a1}, {a2}), v = ({b1}, {b2}). Find the x-component of u + v", a1 + b1, 18,
                "Add the first components.",
                $"{a1} + {b1} = {a1 + b1}.");
        }

        private static Question VectorDotProduct2D()
        {
            int a1 = RandInt(-10, 10);
            int a2 = RandInt(-10, 10);
            int b1 = RandInt(-10, 10);
            int b2 = RandInt(-10, 10);
            int answer = a1 * b1 + a2 * b2;
            return MakeQuestion($"u = ({a1}, {a2}), v = ({b1}, {b2}). Find u . v (dot product)", answer, 20,
                "Multiply corresponding components and add them.",
                $"Calculate ({a1} x {b1}) + ({a2} x {b2}).");
        }

        private static Question VectorMagnitude2D()
        {
            int a1 = RandInt(-12, 12);
            int a2 = RandInt(-12, 12);
            double answer = Math.Sqrt(a1 * a1 + a2 * a2);
            return MakeQuestion($"Find the magnitude of vector u = ({a1}, {a2})", answer, 22,
                "Use sqrt(x^2 + y^2).",
                $"Calculate sqrt({a1}^2 + {a2}^2).");
        }

        private static Question MatrixDeterminant2x2()
        {
            int a = RandInt(-8, 8);
            int b = RandInt(-8, 8);
            int c = RandInt(-8, 8);
            int d = RandInt(-8, 8);
            int answer = a * d - b * c;
            return MakeQuestion($"Determinant of matrix [[{a}, {b}], [{c}, {d}]]", answer, 25,
                "For a 2x2 matrix use ad - bc.",
                $"Calculate ({a} x {d}) - ({b} x {c}).");
        }

        private static Question MatrixAdditionEntry()
        {
            int a11 = RandInt(-9, 9);
            int a12 = RandInt(-9, 9);
            int b11 = RandInt(-9, 9);
            int b12 = RandInt(-9, 9);
            return MakeQuestion(
                $"A = [[{a11}, {a12}], [.., ..]], B = [[{b11}, {b12}], [.., ..]]. Find entry (1,1) of A + B", a11 + b11, 20,
                "Add the matching entries.",
                $"The (1,1) entry is {a11} + {b11}.");
        }

        // -------------------------------------------------------------------
        // Statistics
        // -------------------------------------------------------------------

        private static List<int> GenerateSmallDataSet(int count, int minValue, int maxValue)
        {
            var data = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                data.Add(RandInt(minValue, maxValue));
            }
            return data;
        }

        private static Question MeanOfList()
        {
            List<int> data = GenerateSmallDataSet(5, 1, 50);
            int total = data.Sum();
            double answer = (double)total / data.Count;
            return MakeQuestion($"Mean of {{{string.Join(", ", data)}}}", answer, 25,
                "Add all values and divide by the number of values.",
                $"The total is {total} and there are {data.Count} values.");
        }

        private static Question RangeOfList()
        {
            List<int> data = GenerateSmallDataSet(5, 1, 50);
            int minVal = data.Min();
            int maxVal = data.Max();
            int answer = maxVal - minVal;
            return MakeQuestion($"Range of {{{string.Join(", ", data)}}}", answer, 20,
                "Range = maximum - minimum.",
                $"{maxVal} - {minVal} = {answer}.");
        }

        private static Question MedianOfList()
        {
            List<int> data = GenerateSmallDataSet(5, 1, 50); // Odd count keeps the median a single value
            List<int> sortedData = data.OrderBy(v => v).ToList();
            int answer = sortedData[sortedData.Count / 2];
            return MakeQuestion($"Median of {{{string.Join(", ", data)}}}", answer, 25,
                "Sort the values and choose the middle value.",
                $"Sorted values: {string.Join(", ", sortedData)}.");
        }

        private static Question SimpleProbability()
        {
            int threshold = RandInt(1, 5);
            int favorable = 6 - threshold; // Outcomes on a six-sided die greater than the threshold
            double answer = RoundHalfEven(favorable / 6.0, 3);
            return MakeQuestion($"Rolling a fair six-sided die, what is P(roll > {threshold})? (as a decimal)", answer, 20,
                "Count the favourable outcomes and divide by 6.",
                $"There are {favorable} favourable outcomes out of 6.");
        }

        private static Question ModeOfList()
        {
            List<int> data = GenerateSmallDataSet(7, 1, 6);
            // Force a repeated value so a mode always exists.
            int mode = RandInt(1, 6);
            data[0] = mode;
            data[1] = mode;
            data[2] = mode;
            return MakeQuestion($"Find the mode of {{{string.Join(", ", data)}}}", mode, 25,
                "The mode is the value that appears most often.",
                $"The value {mode} appears more often than the others.");
        }

        private static Question ProbabilityAsPercentage()
        {
            int favourable = RandInt(1, 9);
            const int total = 10;
            int answer = favourable * 10;
            return MakeQuestion($"A probability is {favourable}/{total}. Express it as a percentage.", answer, 18,
                "Multiply the decimal probability by 100.",
                $"{favourable}/{total} = {answer}%.");
        }

        // -------------------------------------------------------------------
        // Complex Numbers
        // -------------------------------------------------------------------

        private static Question ComplexAdditionComponent()
        {
            int a1 = RandInt(-10, 10);
            int b1 = RandInt(-10, 10);
            int a2 = RandInt(-10, 10);
            int b2 = RandInt(-10, 10);
            return MakeQuestion($"z1 = {a1} + {b1}i, z2 = {a2} + {b2}i. Find the real part of z1 + z2", a1 + a2, 18,
                "Add the real parts together.",
                $"{a1} + {a2} = {a1 + a2}.");
        }

        private static Question ComplexModulus()
        {
            int a = RandInt(-12, 12);
            int b = RandInt(-12, 12);
            double answer = Math.Sqrt(a * a + b * b);
            return MakeQuestion($"Find the modulus |z| of z = {a} + {b}i", answer, 22,
                "Use |z| = sqrt(a^2 + b^2).",
                $"sqrt({a}^2 + {b}^2) = {Fixed(answer, 2)}.");
        }

        private static Question ComplexMultiplicationComponent()
        {
            int a1 = RandInt(-9, 9);
            int b1 = RandInt(-9, 9);
            int a2 = RandInt(-9, 9);
            int b2 = RandInt(-9, 9);
            // (a1 + b1 i)(a2 + b2 i) = (a1*a2 - b1*b2) + (a1*b2 + a2*b1) i
            int real = a1 * a2 - b1 * b2;
            return MakeQuestion($"z1 = {a1} + {b1}i, z2 = {a2} + {b2}i. Find the real part of z1 * z2", real, 28,
                "Use FOIL: real part = a1*a2 - b1*b2.",
                $"({a1} x {a2}) - ({b1} x {b2}) = {real}.");
        }

        private static Question ComplexConjugateModulusSquared()
        {
            int a = RandInt(-10, 10);
            int b = RandInt(-10, 10);
            int answer = a * a + b * b;
            return MakeQuestion($"z = {a} + {b}i. Find z times its complex conjugate (a real number)", answer, 28,
                "z * conj(z) = a^2 + b^2.",
                $"{a}^2 + {b}^2 = {answer}.");
        }

        private static Question ComplexPowerDeMoivre()
        {
            int r = RandInt(1, 4);
            int angleDeg = Choice(new[] { 0, 30, 45, 60, 90 });
            int n = RandInt(2, 4);
            double theta = angleDeg * (Math.PI / 180.0);
            // De Moivre's theorem: z^n = r^n * (cos(n*theta) + i*sin(n*theta))
            double answer = IntPow(r, n) * Math.Cos(n * theta);
            return MakeQuestion(
                $"z has modulus {r} and argument {angleDeg} degrees. Find the real part of z^{n} (De Moivre's theorem)",
                answer, 38,
                "Use r^n * cos(n*theta).",
                $"Real part = {r}^{n} * cos({n} * {angleDeg}deg) \u2248 {Fixed(answer, 3)}.");
        }

        // -------------------------------------------------------------------
        // Sequences & Series
        // -------------------------------------------------------------------

        private static Question ArithmeticSequenceNthTerm()
        {
            int a1 = RandInt(1, 20);
            int d = RandInt(-5, 5);
            int n = RandInt(5, 20);
            int answer = a1 + (n - 1) * d;
            return MakeQuestion($"Arithmetic sequence: a1 = {a1}, common difference {d}. Find a{n}", answer, 20,
                "Use a_n = a1 + (n - 1) * d.",
                $"{a1} + ({n - 1}) * {d} = {answer}.");
        }

        private static Question GeometricSequenceNthTerm()
        {
            int a1 = RandInt(1, 5);
            int r = RandInt(2, 4);
            int n = RandInt(3, 7);
            int answer = a1 * IntPow(r, n - 1);
            return MakeQuestion($"Geometric sequence: a1 = {a1}, common ratio {r}. Find a{n}", answer, 24,
                "Use a_n = a1 * r^(n-1).",
                $"{a1} * {r}^{n - 1} = {answer}.");
        }

        private static Question ArithmeticSeriesSum()
        {
            int a1 = RandInt(1, 20);
            int d = RandInt(1, 6);
            int n = RandInt(5, 15);
            double answer = n / 2.0 * (2 * a1 + (n - 1) * d);
            return MakeQuestion(
                $"Sum of the first {n} terms of an arithmetic sequence with a1 = {a1} and common difference {d}", answer, 28,
                "Use S_n = n/2 * (2*a1 + (n-1)*d).",
                $"{n}/2 * (2*{a1} + {n - 1}*{d}) = {PyFloat(answer)}.");
        }

        private static Question GeometricSeriesSum()
        {
            int a1 = RandInt(1, 5);
            int r = RandInt(2, 3);
            int n = RandInt(3, 6);
            double answer = a1 * (IntPow(r, n) - 1) / (double)(r - 1);
            return MakeQuestion(
                $"Sum of the first {n} terms of a geometric sequence with a1 = {a1} and common ratio {r}", answer, 30,
                "Use S_n = a1 * (r^n - 1) / (r - 1).",
                $"{a1} * ({r}^{n} - 1) / ({r} - 1) = {PyFloat(answer)}.");
        }

        private static Question InfiniteGeometricSeriesSum()
        {
            int a1 = RandInt(1, 10);
            int denominator = RandInt(2, 5); // ratio r = 1 / denominator, so |r| < 1 and the series converges
            double r = 1.0 / denominator;
            double answer = a1 / (1 - r);
            return MakeQuestion($"Sum to infinity of a geometric series with a1 = {a1} and common ratio 1/{denominator}", answer, 32,
                "Use S = a1 / (1 - r) when |r| < 1.",
                $"{a1} / (1 - 1/{denominator}) = {Fixed(answer, 2)}.");
        }

        // -------------------------------------------------------------------
        // Output formatting and parsing helpers
        // -------------------------------------------------------------------

        /// <summary>Formats an answer for display, dropping unnecessary decimal zeros.</summary>
        private static string FormatAnswer(double value)
        {
            if (Math.Abs(value - Math.Round(value)) < 0.000001)
            {
                return Fixed(value, 0);
            }
            return Fixed(value, 3);
        }

        /// <summary>Fixed-point formatting with round-half-to-even, matching Python's "{:.Nf}".</summary>
        private static string Fixed(double value, int digits)
        {
            return RoundHalfEven(value, digits).ToString("F" + digits, CultureInfo.InvariantCulture);
        }

        /// <summary>Formats a double the way Python's str(float) does (e.g. 2.0, 0.1, 0.125).</summary>
        private static string PyFloat(double value)
        {
            if (value == Math.Floor(value) && Math.Abs(value) < 1e16)
            {
                return value.ToString("F1", CultureInfo.InvariantCulture);
            }
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static double RoundHalfEven(double value, int digits) =>
            Math.Round(value, digits, MidpointRounding.ToEven);

        private static int? TryParseInt(string? text)
        {
            if (text != null && int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return value;
            }
            return null;
        }

        private static double? TryParseDouble(string? text)
        {
            if (text != null && double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                return value;
            }
            return null;
        }

        /// <summary>Reads a line from the console; exits cleanly if input is closed (EOF).</summary>
        private static string Input()
        {
            string? line = Console.ReadLine();
            if (line == null)
            {
                Environment.Exit(0);
            }
            return line;
        }

        private static void ClearScreen()
        {
            try
            {
                Console.Clear();
            }
            catch (IOException)
            {
                // Output is redirected; nothing to clear.
            }
        }

        // -------------------------------------------------------------------
        // Math helpers
        // -------------------------------------------------------------------

        /// <summary>Random integer in [min, max] inclusive (like Python's random.randint).</summary>
        private static int RandInt(int min, int max) => Randomizer.Next(min, max + 1);

        private static int Choice(int[] options) => options[Randomizer.Next(options.Length)];

        private static int Gcd(int a, int b)
        {
            a = Math.Abs(a);
            b = Math.Abs(b);
            while (b != 0)
            {
                (a, b) = (b, a % b);
            }
            return a;
        }

        private static int IntPow(int baseValue, int exponent)
        {
            int result = 1;
            for (int i = 0; i < exponent; i++)
            {
                result *= baseValue;
            }
            return result;
        }

        private static int ModPow(int baseValue, int exponent, int modulus)
        {
            long result = 1 % modulus;
            long b = baseValue % modulus;
            for (int i = 0; i < exponent; i++)
            {
                result = result * b % modulus;
            }
            return (int)result;
        }

        private static long Factorial(int n)
        {
            long result = 1;
            for (int i = 2; i <= n; i++)
            {
                result *= i;
            }
            return result;
        }

        private static long Permutations(int n, int r)
        {
            long result = 1;
            for (int i = 0; i < r; i++)
            {
                result *= n - i;
            }
            return result;
        }

        private static long Combinations(int n, int r)
        {
            long result = 1;
            for (int i = 1; i <= r; i++)
            {
                result = result * (n - r + i) / i;
            }
            return result;
        }

        // -------------------------------------------------------------------
        // Question factory helper
        // -------------------------------------------------------------------

        /// <summary>Creates a Question with computed points based on the time limit.</summary>
        private static Question MakeQuestion(
            string prompt,
            double answer,
            int timeLimit,
            string hint = "",
            string explanation = "",
            double tolerance = 0.05)
        {
            return new Question
            {
                Prompt = prompt,
                Answer = answer,
                Tolerance = tolerance,
                TimeLimitSeconds = timeLimit,
                Hint = hint,
                Explanation = explanation,
                Points = CalculateBasePoints(timeLimit),
            };
        }

        /// <summary>Determines base points from the time limit (tighter time = higher points).</summary>
        private static int CalculateBasePoints(int timeLimit)
        {
            if (timeLimit <= 15) return 100;
            if (timeLimit <= 20) return 125;
            if (timeLimit <= 25) return 150;
            return 175;
        }
    }
}
