﻿using Xunit.Abstractions;

namespace Sylas.RemoteTasks.Test.Socket
{
    public class PipelinesTest
    {
        private readonly ITestOutputHelper _outputHelper;

        public PipelinesTest(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }
    }
}
