// Copyright 2005, 2006 - Morten Nielsen (www.iter.dk)
//
// This file is part of SharpMap.
// SharpMap is free software; you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
// 
// SharpMap is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.

// You should have received a copy of the GNU Lesser General Public License
// along with SharpMap; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA 

// SOURCECODE IS MODIFIED FROM ANOTHER WORK AND IS ORIGINALLY BASED ON GeoTools.NET:
/*
 *  Copyright (C) 2002 Urban Science Applications, Inc. 
 *
 *  This library is free software; you can redistribute it and/or
 *  modify it under the terms of the GNU Lesser General Public
 *  License as published by the Free Software Foundation; either
 *  version 2.1 of the License, or (at your option) any later version.
 *
 *  This library is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 *  Lesser General Public License for more details.
 *
 *  You should have received a copy of the GNU Lesser General Public
 *  License along with this library; if not, write to the Free Software
 *  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 *
 */

#region Using

using SharpMap.Converters.WellKnownText.IO;
using System;
using System.IO;

#endregion

namespace SharpMap.Converters.WellKnownText
{
    /// <summary>
    /// Reads a stream of Well Known Text (wkt) string and returns a stream of tokens.
    /// </summary>
    internal class WktStreamTokenizer : StreamTokenizer
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the WktStreamTokenizer class.
        /// </summary>
        /// <remarks>The WktStreamTokenizer class aids in reading WKT streams.</remarks>
        /// <param name="reader">A TextReader that contains </param>
        public WktStreamTokenizer(TextReader reader) : base(reader, true)
        {
            if (reader == null)
            {
                throw new ArgumentNullException("reader");
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Reads a token and checks it is what is expected.
        /// </summary>
        /// <param name="expectedToken">The expected token.</param>
        internal void ReadToken(string expectedToken)
        {
            NextToken();
            if (GetStringValue() != expectedToken)
            {
                throw new Exception(String.Format(Map.NumberFormatEnUs,
                                                  "Expecting ('{3}') but got a '{0}' at line {1} column {2}.",
                                                  GetStringValue(), LineNumber, Column, expectedToken));
            }
        }

        /// <summary>
        /// Reads a string inside double quotes.
        /// </summary>
        /// <remarks>
        /// White space inside quotes is preserved.
        /// </remarks>
        /// <returns>The string inside the double quotes.</returns>
        public string ReadDoubleQuotedWord()
        {
            string word = "";
            ReadToken("\"");
            NextToken(false);
            while (GetStringValue() != "\"")
            {
                word = word + GetStringValue();
                NextToken(false);
            }
            return word;
        }

        /// <summary>
        /// Reads the authority and authority code.
        /// </summary>
        /// <param name="authority">String to place the authority in.</param>
        /// <param name="authorityCode">String to place the authority code in.</param>
        public void ReadAuthority(ref string authority, ref long authorityCode)
        {
            //AUTHORITY["EPGS","9102"]]
            if (GetStringValue() != "AUTHORITY")
                ReadToken("AUTHORITY");
            ReadToken("[");
            authority = ReadDoubleQuotedWord();
            ReadToken(",");
            long.TryParse(ReadDoubleQuotedWord(), out authorityCode);
            ReadToken("]");
        }

        #endregion
    }
}