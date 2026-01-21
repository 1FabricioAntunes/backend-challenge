import { useState, FormEvent, ChangeEvent } from 'react';
import { useAuth } from '../context/AuthContext';
import { authApi } from '../services/api';
import '../styles/Login.css';

/**
 * Login Component
 * 
 * Provides authentication interface for users to log in to the application.
 * 
 * Features:
 * - Email and password input fields
 * - Client-side validation
 * - Error handling and display
 * - Loading states
 * - Integration with AuthContext
 * - Accessible form with ARIA labels
 * 
 * Security Notes:
 * - Client-side validation is for UX only
 * - Backend performs authoritative authentication
 * - Tokens stored in localStorage (see security documentation)
 */
export default function LoginComponent() {
  const { login } = useAuth();
  const [email, setEmail] = useState<string>('');
  const [password, setPassword] = useState<string>('');
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [emailError, setEmailError] = useState<string | null>(null);
  const [passwordError, setPasswordError] = useState<string | null>(null);

  /**
   * Validate email format
   */
  const validateEmail = (value: string): boolean => {
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return emailRegex.test(value);
  };

  /**
   * Validate form inputs
   */
  const validateForm = (): boolean => {
    let isValid = true;

    // Validate email
    if (!email.trim()) {
      setEmailError('Email is required');
      isValid = false;
    } else if (!validateEmail(email)) {
      setEmailError('Please enter a valid email address');
      isValid = false;
    } else {
      setEmailError(null);
    }

    // Validate password
    if (!password) {
      setPasswordError('Password is required');
      isValid = false;
    } else if (password.length < 6) {
      setPasswordError('Password must be at least 6 characters');
      isValid = false;
    } else {
      setPasswordError(null);
    }

    return isValid;
  };

  /**
   * Handle email input change
   */
  const handleEmailChange = (e: ChangeEvent<HTMLInputElement>) => {
    const value = e.target.value;
    setEmail(value);
    setError(null);
    // Clear email error when user starts typing
    if (emailError && value.trim()) {
      setEmailError(null);
    }
  };

  /**
   * Handle password input change
   */
  const handlePasswordChange = (e: ChangeEvent<HTMLInputElement>) => {
    const value = e.target.value;
    setPassword(value);
    setError(null);
    // Clear password error when user starts typing
    if (passwordError && value) {
      setPasswordError(null);
    }
  };

  /**
   * Handle form submission
   */
  const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setError(null);

    // Validate form
    if (!validateForm()) {
      return;
    }

    setIsLoading(true);

    try {
      // Call login API
      const response = await authApi.login(email.trim(), password);

      // Update auth context with user and token
      login(
        {
          name: response.user.name,
          email: response.user.email,
        },
        response.token
      );

      // Success - AuthContext will handle state update
      // No need to redirect, App.tsx will handle showing main content
    } catch (err: unknown) {
      // Handle error
      if (err && typeof err === 'object' && 'message' in err) {
        setError(String(err.message));
      } else {
        setError('Login failed. Please check your credentials and try again.');
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="login-container">
      <div className="login-card">
        <header className="login-header">
          <h1 className="login-title">Transaction Processor</h1>
          <p className="login-subtitle">Sign in to your account</p>
        </header>

        <form className="login-form" onSubmit={handleSubmit} noValidate>
          {/* Email Field */}
          <div className="form-group">
            <label htmlFor="email" className="form-label">
              Email Address
            </label>
            <input
              id="email"
              type="email"
              className={`form-input ${emailError ? 'form-input--error' : ''}`}
              placeholder="you@example.com"
              value={email}
              onChange={handleEmailChange}
              disabled={isLoading}
              autoComplete="email"
              aria-invalid={!!emailError}
              aria-describedby={emailError ? 'email-error' : undefined}
            />
            {emailError && (
              <span id="email-error" className="form-error" role="alert">
                {emailError}
              </span>
            )}
          </div>

          {/* Password Field */}
          <div className="form-group">
            <label htmlFor="password" className="form-label">
              Password
            </label>
            <input
              id="password"
              type="password"
              className={`form-input ${passwordError ? 'form-input--error' : ''}`}
              placeholder="Enter your password"
              value={password}
              onChange={handlePasswordChange}
              disabled={isLoading}
              autoComplete="current-password"
              aria-invalid={!!passwordError}
              aria-describedby={passwordError ? 'password-error' : undefined}
            />
            {passwordError && (
              <span id="password-error" className="form-error" role="alert">
                {passwordError}
              </span>
            )}
          </div>

          {/* General Error Message */}
          {error && (
            <div className="form-error-message" role="alert">
              <span className="error-icon" aria-hidden="true">⚠</span>
              <span>{error}</span>
            </div>
          )}

          {/* Submit Button */}
          <button
            type="submit"
            className="login-button"
            disabled={isLoading}
            aria-busy={isLoading}
          >
            {isLoading ? 'Signing in...' : 'Sign In'}
          </button>
        </form>

        {/* Demo/Development Note */}
        <div className="login-footer">
          <div className="login-credentials">
            <p className="login-credentials-title">Test Credentials:</p>
            <div className="login-credentials-info">
              <div className="credential-item">
                <span className="credential-label">E-mail:</span>
                <span className="credential-value">test@transactionprocessor.local</span>
              </div>
              <div className="credential-item">
                <span className="credential-label">Password:</span>
                <span className="credential-value">TestPassword123!</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
