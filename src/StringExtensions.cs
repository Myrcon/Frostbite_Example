/* 
 * Copyright (C) 2013 Myrcon Pty. Ltd. / Geoff "Phogue" Green
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to 
 * deal in the Software without restriction, including without limitation the
 * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
 * sell copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in 
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
 * IN THE SOFTWARE.
*/
using System;
using System.Collections.Generic;

namespace Frostbite_Example {
    public static class StringExtensions {

        /// <summary>
        /// This method accepts a string such as "Hello World!" and then splits them up into words
        /// 
        /// "Hello World!" => List { "Hello", "World!" }
        /// "Hello \"There World!\"" => List { "Hello", "There World!" }
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public static List<string> Wordify(this string command) {
            List<string> returnList = new List<string>();

            string fullWord = String.Empty;
            int quoteStack = 0;
            bool isEscaped = false;

            foreach (char input in command) {

                if (input == ' ') {
                    if (quoteStack == 0) {
                        returnList.Add(fullWord);
                        fullWord = String.Empty;
                    }
                    else {
                        fullWord += ' ';
                    }
                }
                else if (input == 'n' && isEscaped == true) {
                    fullWord += '\n';
                    isEscaped = false;
                }
                else if (input == 'r' && isEscaped == true) {
                    fullWord += '\r';
                    isEscaped = false;
                }
                else if (input == 't' && isEscaped == true) {
                    fullWord += '\t';
                    isEscaped = false;
                }
                else if (input == '"') {
                    if (isEscaped == false) {
                        if (quoteStack == 0) {
                            quoteStack++;
                        }
                        else {
                            quoteStack--;
                        }
                    }
                    else {
                        fullWord += '"';
                    }
                }
                else if (input == '\\') {
                    if (isEscaped == true) {
                        fullWord += '\\';
                        isEscaped = false;
                    }
                    else {
                        isEscaped = true;
                    }
                }
                else {
                    fullWord += input;
                    isEscaped = false;
                }
            }

            returnList.Add(fullWord);

            return returnList;
        }
    }
}