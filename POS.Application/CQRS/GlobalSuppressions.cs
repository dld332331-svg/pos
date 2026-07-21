// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to the CQRS project.
using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "CQRS handlers receive parameters from the dispatcher which validates existence at runtime", Scope = "namespaceanddescendants", Target = "~N:POS.Application.CQRS")]
