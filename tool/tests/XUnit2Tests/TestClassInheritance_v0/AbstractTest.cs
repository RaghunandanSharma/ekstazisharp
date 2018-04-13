﻿// Copyright (c) 2017, Marko Vasic
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Xunit;

namespace TestClassInheritance
{
    public abstract class AbstractTest
    {
        [Fact]
        public void Method()
        {
        }

        protected abstract void DoTest();
    }

    public class TestA : AbstractTest
    {
        protected override void DoTest()
        {
        }
    }

    public abstract class TestB : AbstractTest { }

    public class TestC : TestB
    {
        protected override void DoTest()
        {
        }
    }
}
