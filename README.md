Technical Analysis Report: Math Quiz Application (Program.cs)
Executive Summary

This report analyses the structure, architecture, algorithms, and object-oriented design of the Math Quiz application contained in Program.cs. The application is a feature-rich C# console program that generates mathematics questions across multiple disciplines, tracks performance, and persists statistics between sessions.

System Overview
The application uses a menu-driven console interface. A user selects category, difficulty, and question count. The application generates questions dynamically, evaluates answers, awards points, records statistics and displays results.


Architecture
The program follows a modular procedural architecture with lightweight domain models. Core classes are Question, QuizResult and CategoryStats. The Program class orchestrates all application behaviour.


Detailed Analysis of Main()
Main() performs system initialisation. It configures culture settings, builds category data structures, loads statistics from storage, displays the menu, and routes user choices using a loop and switch statement. This method controls the entire application lifecycle.


Detailed Analysis of RunQuiz()
RunQuiz() is the core engine. It generates a question, starts a Stopwatch timer, receives input, validates answers, manages hints, updates streaks, calculates scores, stores review information, updates category statistics and returns a QuizResult object.


Question Generation System
Questions are generated using delegate-based factories. Each category contains a List<Func<Question>>. Instead of storing fixed questions, functions create new randomised questions every time they are called, providing virtually unlimited variation.


Difficulty Algorithm
FilterByDifficulty() divides each category pool into thirds. Easy uses the first section, Medium uses the middle section and Hard uses the final section. GenerateQuestion() also uses quiz progress to gradually increase challenge.


Statistics Persistence
Statistics are stored in a text file. LoadStatistics() reconstructs state from disk while SaveStatistics() serialises current values. This provides persistence across sessions.


Data Structures
The application makes extensive use of List<T>, Dictionary<TKey,TValue>, delegates (Func<Question>), and domain objects. The category dictionary is the most important structure because it maps category names to generator methods.


Object Oriented Design
Question encapsulates quiz question information. QuizResult encapsulates outcome information. CategoryStats encapsulates long-term performance statistics. This demonstrates encapsulation and separation of concerns.


Software Engineering Evaluation
Strengths include modularity, scalability, validation, persistence, reusable helper functions and strong separation of responsibilities. Improvements could include multiple source files, JSON persistence, enums and unit testing.


Annotated Code Example: Main()
public static void Main()
{
    BuildCategories();
    LoadStatistics();

    bool running = true;

    while (running)
    {
        // display menu and process selection
    }
}

Analysis:
BuildCategories() creates category collections. LoadStatistics() restores saved data. The while loop keeps the application running until Exit is selected.


Annotated Code Example: GenerateQuestion()
List<Func<Question>> pool = categories[category];
List<Func<Question>> filteredPool = FilterByDifficulty(pool, difficulty);
int targetIndex = (int)Math.Round(progress * (filteredPool.Count - 1));
return filteredPool[targetIndex]();

Analysis:
1. Select category.
2. Apply difficulty filter.
3. Calculate progress-based difficulty.
4. Execute generator delegate and return a Question object.
Annotated Code Example: Answer Validation
bool isCorrect =
    Math.Abs(userAnswer - question.Answer)
    <= question.Tolerance;

Analysis:
Tolerance-based comparison avoids floating-point precision problems and is more reliable than direct equality checks.
References
1. Program source analysed: Program.cs (user-supplied file in this conversation).
2. Microsoft Learn. C# programming guide, collections, delegates, classes, and file I/O concepts.
3. Microsoft .NET documentation: System.Collections.Generic, System.IO, System.Diagnostics.Stopwatch, and CultureInfo.

