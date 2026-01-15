# Frontend

This document describes the frontend implementation, responsibilities, and technical decisions.

## Technology Stack

- **Framework**: React 19+ with TypeScript
- **Styling**: Custom CSS (aligned with https://www.lettrlabs.com)
- **Restrictions**:
  - No jQuery
  - No Bootstrap
- **Build Tool**: Vite
- **State Management**: React Hooks / Context API
- **HTTP Client**: Axios

## Backend Architecture Context

The frontend interacts with a **serverless, asynchronous backend** architecture:

- **API**: ASP.NET Core API provides RESTful endpoints
- **File Processing**: AWS Lambda processes files asynchronously via SQS queue
- **Storage**: S3 for file storage, PostgreSQL for transactional data
- **Non-Blocking**: File upload returns immediately (HTTP 202 Accepted); processing happens asynchronously

This architecture is critical for the frontend design:
- Upload operations return immediately without waiting for processing
- File status must be polled or refreshed to check processing completion
- The UI never blocks on file processing

See [architecture.md](architecture.md) for complete system architecture details.

## Responsibilities

The frontend is responsible for:

1. **File Upload**
   - CNAB file upload interface
   - File type validation
   - Upload progress indication

2. **File Management**
   - Display list of uploaded files
   - Show file processing status
   - Display processing errors

3. **Transaction Display**
   - List transactions with filters
   - Store-based filtering
   - Date range filtering
   - Transaction details

4. **User Experience**
   - Non-blocking operations
   - Responsive design
   - Real-time status updates
   - Error handling and feedback

## Design Principles

### No Blocking Operations

**Critical**: The UI must never block waiting for file processing.

- File upload returns immediately
- Status polling for processing updates
- Optimistic UI updates where appropriate
- Clear loading states

### Responsive Design

- Mobile-friendly layout
- Adaptive to different screen sizes
- Touch-friendly interactions
- Accessible design

### Custom Styling

- No Bootstrap dependency
- No jQuery dependency
- Custom CSS aligned with TransactionProcessor branding
- Modern, clean design

## Component Structure

### Actual Current Structure (KISS/YAGNI)

The current implementation uses a simple tab-based navigation within `App.tsx`:

```
frontend/
├── src/
│   ├── App.tsx              # Main application component with tab navigation
│   ├── main.tsx             # Entry point
│   ├── index.css            # Global styles
│   ├── components/
│   │   ├── CnabUpload.tsx   # File upload component
│   │   └── TransactionsList.tsx # Transactions display component
│   ├── services/
│   │   └── api.ts           # API client (Axios)
│   ├── types/               # TypeScript type definitions (when needed)
│   ├── hooks/               # Custom hooks (when needed)
│   ├── utils/               # Utility functions (when needed)
│   └── assets/              # Static assets
├── package.json
├── tsconfig.json
├── vite.config.ts
└── Dockerfile
```

**Approach**: Minimal, pragmatic structure following **KISS and YAGNI principles**
- Only create files/folders when actually needed
- 2 main components, 1 API service file
- Tab-based navigation in `App.tsx`
- No unused abstractions

### When to Expand

**Add structure only when you actually need it (YAGNI)**:
- Create individual hook files when you have reusable stateful logic
- Create utility files when you have shared helper functions
- Create `pages/` directory when you need 4+ distinct views requiring routing
- Add React Router when you need URL-based navigation, deep linking, or route parameters

## Routing and Navigation

### Current Approach

State-based tab navigation (see `App.tsx`):
- Suitable for 2-3 views
- No routing library required
- KISS principle applied

### Migration Threshold

Consider adding React Router when you have:
- 4+ distinct views/pages
- Need for URL-based navigation (shareable, bookmarkable URLs)
- Route parameters (e.g., `/transactions/:id`)
- Browser history requirements

**When/if migrating**: Refer to React Router documentation, not documentation examples (to avoid outdated code).

## Key Components

### FileUpload Component

- Drag-and-drop support
- File type validation (.txt, CNAB format)
- File size validation
- Upload progress indication
- Error handling

### FileList Component

- Display uploaded files
- Show processing status
- Status badges (Uploaded, Processing, Processed, Rejected)
- Error messages display
- Refresh functionality

### TransactionList Component

- Display transactions in table/card format
- Pagination support
- Sorting capabilities
- Loading states
- Empty states

### Filters Component

- Store name filter
- Date range picker
- Clear filters button
- Filter state management

## Validation

### Client-Side Validation

The frontend performs basic validation:

1. **File Type Validation**
   - Must be .txt file
   - Basic format check

2. **File Size Validation**
   - Maximum file size check
   - User-friendly error messages

3. **Form Validation**
   - Required fields
   - Date range validation
   - Input format validation

**Note**: Client-side validation is for UX only. Backend performs authoritative validation.

## State Management

### React Hooks

- `useState` for local component state
- `useEffect` for side effects
- `useContext` for shared state (if needed)
- Custom hooks for reusable logic

### State Structure

**Pattern**: Local component state with hooks (`useState`, `useEffect`)

**Type Definitions**: See `src/types/index.ts`

**State Management Principles**:
- Component-local state for UI concerns (loading, errors)
- Prop drilling acceptable for current scale
- Consider Context API if state sharing becomes complex
- Custom hooks for reusable stateful logic
```

## API Integration

### API Service

**Pattern**: Centralized API client using Axios

**Implementation**: See `src/services/api.ts`

**Key Principles**:
- Single axios instance with base configuration
- Environment-based API URL (`VITE_API_URL`)
- Consistent error handling
- Typed request/response interfaces
- API versioning in URLs (e.g., `/api/files/v1`, `/api/transactions/v1`, `/api/stores/v1`)
```

### Error Handling

- Network error handling
- API error response handling
- User-friendly error messages
- Retry logic for transient errors

## Status Polling

### Polling Strategy

**Pattern**: Custom hook for status polling (see `src/hooks/usePolling.ts` if implemented)

**Requirements**:
- Poll only while status is 'Processing'
- Stop polling on completion (success/error)
- Configurable interval (recommended: 2-5 seconds)
- Cleanup on unmount
- Consider exponential backoff for production
```

### Polling Considerations

- Poll only while processing
- Stop polling on success/error
- Exponential backoff for errors
- Maximum polling duration

## User Experience

### Loading States

- Skeleton screens during loading
- Progress indicators for uploads
- Disabled buttons during operations
- Clear loading messages

### Error Handling

- Inline error messages
- Toast notifications for errors
- Retry mechanisms
- Clear error descriptions

### Success Feedback

- Success messages
- Visual confirmation
- Automatic updates
- Positive reinforcement

## Styling Approach

### Custom CSS

- No CSS framework dependencies
- Modular CSS files
- CSS variables for theming
- Responsive design with media queries

### Design Alignment

- Aligned with TransactionProcessor branding
- Consistent color scheme
- Typography hierarchy
- Spacing system

## Performance Optimization

### Optimization Strategies

- Code splitting
- Lazy loading for routes
- Memoization for expensive computations
- Debouncing for search/filters
- Image optimization (if applicable)

### Bundle Size

- Tree shaking
- Minimal dependencies
- Production builds optimized
- Asset compression

## Accessibility

### Accessibility Features

- Semantic HTML
- ARIA labels where needed
- Keyboard navigation support
- Screen reader compatibility
- Focus management

## Testing

### Frontend Testing

- Component unit tests
- Integration tests
- User interaction tests
- Visual regression tests (if applicable)

### Testing Tools

- Jest for unit testing
- React Testing Library
- E2E tests (happy path)

See [Testing Strategy](testing-strategy.md) for details.

## Documentation Philosophy

**Principle**: Documentation should describe **architectural decisions and patterns**, not implementation details.

**What belongs in docs**:
- ✅ Technology choices and rationale
- ✅ Architectural patterns and structure
- ✅ Design principles and constraints
- ✅ Migration thresholds and decision points

**What belongs in code**:
- ✅ Implementation details
- ✅ Specific function signatures
- ✅ Current component structure
- ✅ Actual type definitions

**Reason**: Code changes frequently; principles change rarely. Outdated code examples mislead developers.

## Build and Deployment

### Development

```bash
pnpm install
pnpm dev
```

**Note**: The project uses `pnpm` as the package manager (specified in package.json).

### Production Build

```bash
pnpm build
```

### Preview Production Build Locally

```bash
pnpm preview
```

### Deployment

- Static files served via CDN
- Environment variables for configuration
- Build optimization enabled

## Browser Support

- Modern browsers (Chrome, Firefox, Safari, Edge)
- ES6+ features
- Fetch API support
- CSS Grid/Flexbox support

## Future Enhancements (Out of Scope)

- Real-time updates via WebSockets
- Advanced filtering UI
- Export functionality
- Bulk operations
- Advanced analytics visualization
