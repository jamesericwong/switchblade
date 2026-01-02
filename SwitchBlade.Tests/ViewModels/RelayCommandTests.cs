using System;
using Xunit;
using SwitchBlade.ViewModels;

namespace SwitchBlade.Tests.ViewModels
{
    public class RelayCommandTests
    {
        [Fact]
        public void Execute_InvokesAction()
        {
            var wasExecuted = false;
            var command = new RelayCommand(_ => wasExecuted = true);

            command.Execute(null);

            Assert.True(wasExecuted);
        }

        [Fact]
        public void Execute_PassesParameterToAction()
        {
            object? receivedParameter = null;
            var command = new RelayCommand(p => receivedParameter = p);
            var expectedParameter = "test parameter";

            command.Execute(expectedParameter);

            Assert.Equal(expectedParameter, receivedParameter);
        }

        [Fact]
        public void CanExecute_WithNoPredicate_ReturnsTrue()
        {
            var command = new RelayCommand(_ => { });

            var result = command.CanExecute(null);

            Assert.True(result);
        }

        [Fact]
        public void CanExecute_WithPredicateReturningTrue_ReturnsTrue()
        {
            var command = new RelayCommand(_ => { }, _ => true);

            var result = command.CanExecute(null);

            Assert.True(result);
        }

        [Fact]
        public void CanExecute_WithPredicateReturningFalse_ReturnsFalse()
        {
            var command = new RelayCommand(_ => { }, _ => false);

            var result = command.CanExecute(null);

            Assert.False(result);
        }

        [Fact]
        public void CanExecute_PredicateReceivesParameter()
        {
            object? receivedParameter = null;
            var command = new RelayCommand(_ => { }, p =>
            {
                receivedParameter = p;
                return true;
            });
            var expectedParameter = "predicate test";

            command.CanExecute(expectedParameter);

            Assert.Equal(expectedParameter, receivedParameter);
        }

        [Fact]
        public void Constructor_WithNullExecute_DoesNotThrow()
        {
            // RelayCommand doesn't validate null in constructor, but will throw NullReferenceException on Execute
            // This test documents the actual behavior
            var exception = Record.Exception(() => new RelayCommand(null!));
            
            Assert.Null(exception); // Constructor allows null
        }

        [Fact]
        public void CanExecuteChanged_CanAddAndRemoveHandler()
        {
            var command = new RelayCommand(_ => { });
            EventHandler handler = (s, e) => { };

            // Should not throw
            command.CanExecuteChanged += handler;
            command.CanExecuteChanged -= handler;
        }
    }
}
