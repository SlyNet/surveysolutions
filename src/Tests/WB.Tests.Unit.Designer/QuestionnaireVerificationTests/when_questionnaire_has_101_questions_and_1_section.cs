using System.Collections.Generic;
using System.Linq;
using Main.Core.Documents;
using Main.Core.Entities.Composite;
using WB.Core.BoundedContexts.Designer.ValueObjects;
using QuestionnaireVerifier = WB.Core.BoundedContexts.Designer.Verifier.QuestionnaireVerifier;

namespace WB.Tests.Unit.Designer.QuestionnaireVerificationTests
{
    internal class when_questionnaire_has_101_questions_and_1_section : QuestionnaireVerifierTestsContext
    {
        [NUnit.Framework.OneTimeSetUp]
        public void context () {
            questionnaire = Create.QuestionnaireDocument(children: new IComposite[]
            {
                Create.Section(
                    children: Enumerable.Range(1, 101).Select(_ => Create.TextQuestion()).ToArray<IComposite>()),
            });

            verifier = CreateQuestionnaireVerifier();
            BecauseOf();
        }

        private void BecauseOf() =>
            messages = verifier.Verify(Create.QuestionnaireView(questionnaire));

        [NUnit.Framework.Test]
        public void should_return_warning_WB0206 () => messages.ShouldContainWarning("WB0206");

        private static QuestionnaireDocument questionnaire;
        private static QuestionnaireVerifier verifier;
        private static IEnumerable<QuestionnaireVerificationMessage> messages;
    }
}