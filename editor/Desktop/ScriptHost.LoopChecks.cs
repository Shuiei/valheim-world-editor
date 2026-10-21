using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TerrainEditor.Desktop;

// Loop checks: a Check call at the top of every loop body and before every goto, so Stop can end the script.
public static partial class ScriptHost
{
    /// <summary>
    /// Puts a call to the API's Check at the start of every loop's body and before every goto in a script, so that
    /// Stop and the 2-minute limit end a script whose own loops call nothing that checks. What it adds holds no line
    /// break: the script keeps its line numbers (for the line an error happens on).
    /// </summary>
    private sealed class LoopChecks : CSharpSyntaxRewriter
    {
        /// <summary>The call added, parsed once.</summary>
        private static readonly StatementSyntax Check = SyntaxFactory.ParseStatement("global::__Host.Check();");

        /// <summary>
        /// The statement with the Check before it, both in a block of their own. The statement's trivia (comments,
        /// line breaks, #if and other directives) goes around the block, not into it: a directive stays at the start
        /// of its own line.
        /// </summary>
        private static BlockSyntax Checked(StatementSyntax statement) =>
            SyntaxFactory.Block(SyntaxFactory.Token(default, SyntaxKind.OpenBraceToken, default), [Check, statement.WithoutTrivia()], SyntaxFactory.Token(default, SyntaxKind.CloseBraceToken, default))
                .WithLeadingTrivia(statement.GetLeadingTrivia()).WithTrailingTrivia(statement.GetTrailingTrivia());

        /// <inheritdoc/>
        public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
        {
            var n = (WhileStatementSyntax)base.VisitWhileStatement(node)!;
            return n.WithStatement(Checked(n.Statement));
        }

        /// <inheritdoc/>
        public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
        {
            var n = (DoStatementSyntax)base.VisitDoStatement(node)!;
            return n.WithStatement(Checked(n.Statement));
        }

        /// <inheritdoc/>
        public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
        {
            var n = (ForStatementSyntax)base.VisitForStatement(node)!;
            return n.WithStatement(Checked(n.Statement));
        }

        /// <inheritdoc/>
        public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
        {
            var n = (ForEachStatementSyntax)base.VisitForEachStatement(node)!;
            return n.WithStatement(Checked(n.Statement));
        }

        /// <inheritdoc/>
        public override SyntaxNode? VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
        {
            var n = (ForEachVariableStatementSyntax)base.VisitForEachVariableStatement(node)!;
            return n.WithStatement(Checked(n.Statement));
        }

        /// <inheritdoc/>
        public override SyntaxNode? VisitGotoStatement(GotoStatementSyntax node)
        {
            return Checked((GotoStatementSyntax)base.VisitGotoStatement(node)!);
        }
    }
}
