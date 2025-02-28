using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Main.Core.Documents;
using WB.Core.BoundedContexts.Designer.ValueObjects;
using QuestionnaireVerifier = WB.Core.BoundedContexts.Designer.Verifier.QuestionnaireVerifier;

namespace WB.Tests.Unit.Designer.QuestionnaireVerificationTests
{
    internal class when_verifying_questionnaire_with_macro_with_invalid_name : QuestionnaireVerifierTestsContext
    {
        [NUnit.Framework.OneTimeSetUp] public void context () {
            questionnaire = Create.QuestionnaireDocument(questionId, Create.TextQuestion(variable: "var"));
            questionnaire.Macros.Add(macroId, Create.Macro("Very invalid! name", "var == \"\""));
            verifier = CreateQuestionnaireVerifier();
            BecauseOf();
        }

        private void BecauseOf() =>
            verificationMessages = verifier.CheckForErrors(Create.QuestionnaireView(questionnaire));

        [NUnit.Framework.Test] public void should_return_1_message () =>
            verificationMessages.Count().Should().Be(1);

        [NUnit.Framework.Test] public void should_return_message_with_code__WB0010 () =>
            verificationMessages.Single().Code.Should().Be("WB0010");

        [NUnit.Framework.Test] public void should_return_message_with_1_references () =>
            verificationMessages.Single().References.Count().Should().Be(1);

        [NUnit.Framework.Test] public void should_return_message_reference_with_type_Macro () =>
            verificationMessages.Single().References.First().Type.Should().Be(QuestionnaireVerificationReferenceType.Macro);

        [NUnit.Framework.Test] public void should_return_message_reference_with_id_of_macroId () =>
            verificationMessages.Single().References.First().Id.Should().Be(macroId);

        private static IEnumerable<QuestionnaireVerificationMessage> verificationMessages;
        private static QuestionnaireVerifier verifier;
        private static QuestionnaireDocument questionnaire;
        private static readonly Guid macroId = Guid.Parse("11111111111111111111111111111111");
        private static readonly Guid questionId = Guid.Parse("10000000000000000000000000000000");
    }
}