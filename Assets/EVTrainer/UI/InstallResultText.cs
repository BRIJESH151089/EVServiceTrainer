using System.Globalization;
using System.Text;
using EVTrainer.Core;

namespace EVTrainer.UI
{
    /// <summary>Short trainee-facing explanation of an install attempt.</summary>
    public static class InstallResultText
    {
        public static string Describe(in InstallResult result, AssemblyDefinition definition)
        {
            var c = CultureInfo.InvariantCulture;
            definition.TryGetPart(result.PartId, out var part);
            string name = part != null ? part.DisplayName : result.PartId;
            var e = result.Evaluation;

            switch (result.Outcome)
            {
                case InstallOutcome.Installed:
                    return $"{name} installed.";
                case InstallOutcome.AlreadyInstalled:
                    return $"{name} is already installed.";
                case InstallOutcome.OutOfTolerance:
                    return string.Format(c, "{0} not seated: {1:0.0} cm from its position (max {2:0.0} cm). Line it up with the outline.",
                        name, e.PositionErrorMetres * 100f, part != null ? part.PositionToleranceMetres * 100f : 0f);
                case InstallOutcome.WrongOrientation:
                    return string.Format(c, "{0} wrong way round: turned {1:0} deg (max {2:0} deg). Rotate it to match the outline.",
                        name, e.AngleErrorDegrees, part != null ? part.AngleToleranceDegrees : 0f);
                case InstallOutcome.WrongOrder:
                    var sb = new StringBuilder();
                    foreach (var id in result.MissingPrerequisites)
                    {
                        if (sb.Length > 0) sb.Append(", ");
                        sb.Append(definition.TryGetPart(id, out var missing) ? missing.DisplayName : id);
                    }
                    return $"Wrong order: fit {sb} before the {name}.";
                default:
                    return result.ToString();
            }
        }
    }
}
