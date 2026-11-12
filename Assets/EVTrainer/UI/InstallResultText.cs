using System.Globalization;
using EVTrainer.Core;

namespace EVTrainer.UI
{
    /// <summary>
    /// Trainee-facing text for an install attempt. Learn mode explains (distances, reasons, hints);
    /// Test mode only says whether the part was accepted.
    /// </summary>
    public static class InstallResultText
    {
        public static string Describe(in InstallResult result, AssemblyDefinition definition,
            TrainingMode mode = TrainingMode.Learn)
        {
            definition.TryGetPart(result.PartId, out var part);
            string name = part != null ? part.DisplayName : result.PartId;
            return mode == TrainingMode.Test ? Short(result, name) : Detailed(result, part, name);
        }

        private static string Short(in InstallResult result, string name)
        {
            switch (result.Outcome)
            {
                case InstallOutcome.Installed: return $"{name} installed.";
                case InstallOutcome.AlreadyInstalled: return $"{name} is already installed.";
                case InstallOutcome.WrongOrder: return "Cannot be fitted yet.";
                default: return "Not fitted correctly.";
            }
        }

        private static string Detailed(in InstallResult result, PartSpec part, string name)
        {
            var c = CultureInfo.InvariantCulture;
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
                    return "Wrong order: " + result.Reason + " Part returned to its place.";
                default:
                    return result.ToString();
            }
        }
    }
}
