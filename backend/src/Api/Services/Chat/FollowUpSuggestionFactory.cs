using Api.Contracts;
using Api.Models;

namespace Api.Services;

internal static class FollowUpSuggestionFactory
{
    private const int MaxSuggestions = 3;
    private const string LanguageEn = "en";
    private const string LanguageEs = "es";
    private const string LanguagePtBr = "pt-BR";
    private const string LanguageFr = "fr";

    public static List<string> Create(
        IReadOnlyList<VectorSearchMatch> matches,
        string? userRole,
        string? responseLanguage,
        string? refusalReason = null)
    {
        var language = NormalizeLanguage(responseLanguage);
        var topic = GetTopic(matches);

        if (matches.Count == 0 || string.Equals(refusalReason, StructuredAnswerDto.NotFoundReason, StringComparison.Ordinal))
        {
            return BuildNoContextSuggestions(language);
        }

        var roleSuggestion = BuildRoleSuggestion(language, userRole, topic);
        var evidenceSuggestion = BuildEvidenceSuggestion(language, topic);
        var actionSuggestion = BuildActionSuggestion(language, topic);

        return new[] { roleSuggestion, evidenceSuggestion, actionSuggestion }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal)
            .Take(MaxSuggestions)
            .ToList();
    }

    private static string NormalizeLanguage(string? responseLanguage)
    {
        if (string.IsNullOrWhiteSpace(responseLanguage))
        {
            return LanguageEn;
        }

        var normalized = responseLanguage.Trim();
        return normalized is LanguageEs or LanguagePtBr or LanguageFr ? normalized : LanguageEn;
    }

    private static string GetTopic(IReadOnlyList<VectorSearchMatch> matches)
    {
        var firstMatch = matches.FirstOrDefault();
        if (firstMatch is null)
        {
            return "this SOP section";
        }

        if (firstMatch.Record.Metadata.TryGetValue("SectionTitle", out var sectionTitle)
            && !string.IsNullOrWhiteSpace(sectionTitle))
        {
            return sectionTitle.Trim();
        }

        return firstMatch.Record.Source;
    }

    private static List<string> BuildNoContextSuggestions(string language)
    {
        return language switch
        {
            LanguageEs =>
            [
                "Puedes reformular la pregunta con mas detalles operativos?",
                "Que seccion del SOP quieres consultar?",
                "Quieres que te muestre una lista de temas relacionados?"
            ],
            LanguagePtBr =>
            [
                "Voce pode reformular a pergunta com mais detalhes operacionais?",
                "Qual secao do SOP voce quer consultar?",
                "Quer que eu liste topicos relacionados?"
            ],
            LanguageFr =>
            [
                "Pouvez-vous reformuler la question avec plus de details operationnels ?",
                "Quelle section du SOP voulez-vous consulter ?",
                "Voulez-vous une liste de sujets connexes ?"
            ],
            _ =>
            [
                "Can you rephrase the question with more operational detail?",
                "Which SOP section should I focus on?",
                "Would you like a list of related SOP topics?"
            ]
        };
    }

    private static string BuildRoleSuggestion(string language, string? userRole, string topic)
    {
        return (language, userRole) switch
        {
            (LanguageEs, "manager") => $"Que riesgos de cumplimiento debo revisar como gerente en {topic}?",
            (LanguageEs, "department_lead") => $"Que tareas debo delegar al equipo para {topic}?",
            (LanguageEs, "cashier") => $"Cual es el procedimiento en caja para {topic}?",
            (LanguagePtBr, "manager") => $"Quais riscos de conformidade devo revisar como gerente em {topic}?",
            (LanguagePtBr, "department_lead") => $"Quais tarefas devo delegar para o time em {topic}?",
            (LanguagePtBr, "cashier") => $"Qual e o procedimento no caixa para {topic}?",
            (LanguageFr, "manager") => $"Quels risques de conformite dois-je verifier comme manager pour {topic} ?",
            (LanguageFr, "department_lead") => $"Quelles taches dois-je deleguer a l'equipe pour {topic} ?",
            (LanguageFr, "cashier") => $"Quelle est la procedure en caisse pour {topic} ?",
            (_, "manager") => $"What compliance risks should a manager watch for in {topic}?",
            (_, "department_lead") => $"What team handoff checklist should a department lead use for {topic}?",
            (_, "cashier") => $"What are the cashier-level steps for {topic}?",
            (LanguageEs, _) => $"Cuales son los pasos clave en {topic}?",
            (LanguagePtBr, _) => $"Quais sao os passos principais em {topic}?",
            (LanguageFr, _) => $"Quelles sont les etapes cles dans {topic} ?",
            _ => $"What are the key steps in {topic}?"
        };
    }

    private static string BuildEvidenceSuggestion(string language, string topic)
    {
        return language switch
        {
            LanguageEs => $"Puedes resumir los puntos mas importantes citados de {topic}?",
            LanguagePtBr => $"Pode resumir os pontos mais importantes citados de {topic}?",
            LanguageFr => $"Pouvez-vous resumer les points les plus importants cites de {topic} ?",
            _ => $"Can you summarize the most important cited points from {topic}?"
        };
    }

    private static string BuildActionSuggestion(string language, string topic)
    {
        return language switch
        {
            LanguageEs => $"Que errores comunes debo evitar al aplicar {topic}?",
            LanguagePtBr => $"Quais erros comuns devo evitar ao aplicar {topic}?",
            LanguageFr => $"Quelles erreurs courantes dois-je eviter en appliquant {topic} ?",
            _ => $"What common mistakes should I avoid when applying {topic}?"
        };
    }
}