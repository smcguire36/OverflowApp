using System;
using System.Collections.Generic;
using System.Text;

namespace Contracts
{
    public record AnswerCountUpdated(string QuestionId, int AnswerCount);
}
