﻿#region BSD 3-Clause License
// <copyright file="TraceEvent.cs" company="Edgerunner.org">
// Copyright 2020 Thaddeus Ryker
// </copyright>
// 
// BSD 3-Clause License
// 
// Copyright (c) 2020, Thaddeus Ryker
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 
// 1. Redistributions of source code must retain the above copyright notice, this
//    list of conditions and the following disclaimer.
// 
// 2. Redistributions in binary form must reproduce the above copyright notice,
//    this list of conditions and the following disclaimer in the documentation
//    and/or other materials provided with the distribution.
// 
// 3. Neither the name of the copyright holder nor the names of its
//    contributors may be used to endorse or promote products derived from
//    this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
// CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

using Antlr4.Runtime;

namespace Org.Edgerunner.ANTLR4.Tools.Testing.GrunWin.Tracing
{
   /// <summary>
   /// Struct that represents a parser trace event.
   /// </summary>
   public struct TraceEvent
   {
      /// <summary>
      /// Initializes a new instance of the <see cref="TraceEvent"/> struct.
      /// </summary>
      /// <param name="type">The type.</param>
      /// <param name="token">The token.</param>
      /// <param name="context">The parser rule context.</param>
      /// <param name="parserRule">The parser rule.</param>
      public TraceEvent(TraceEventType type, IToken token, ParserRuleContext context, string parserRule)
      {
         Type = type;
         Token = token;
         ParserRuleContext = context;
         ParserRule = parserRule;
      }

      /// <summary>
      /// Gets the type.
      /// </summary>
      /// <value>The type.</value>
      public TraceEventType Type { get; }

      /// <summary>
      /// Gets the token.
      /// </summary>
      /// <value>The token.</value>
      public IToken Token { get; }

      /// <summary>
      /// Gets the token text.
      /// </summary>
      /// <value>The token text.</value>
      public string TokenText => (Token != null) ? Token.Text : string.Empty;

      /// <summary>
      /// Gets the parser rule context.
      /// </summary>
      /// <value>The parser rule context.</value>
      public ParserRuleContext ParserRuleContext { get; }

      /// <summary>
      /// Gets the parser rule.
      /// </summary>
      /// <value>The parser rule.</value>
      public string ParserRule { get; }
   }
}