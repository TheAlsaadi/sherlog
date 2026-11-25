using System.CommandLine;

namespace Sherlog.Cli.Commands;

/// <summary><c>sherlog recipes check</c>: validate a recipe library.</summary>
internal static class RecipesCommand
{
    public static Command Create(CliEnvironment env)
    {
        var library = CommonOptions.Library();
        var check = new Command("check", "Check that every recipe is valid.") { library };
        check.SetAction(result => CliApp.Guard(env, () =>
        {
            var loaded = Analysis.LoadRecipes(env, result.GetValue(library));
            foreach (var error in loaded.Errors)
            {
                env.Error.WriteLine($"{error.FilePath}: {error.Message}");
            }

            if (loaded.Errors.Count > 0)
            {
                return 1;
            }

            var count = loaded.Recipes.Count;
            env.Out.WriteLine(count == 1 ? "1 recipe OK" : $"{count} recipes OK");
            return 0;
        }));

        return new Command("recipes", "Work with the recipe library.") { check };
    }
}
