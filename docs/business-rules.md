# Business Rules

This document describes the functional requirements and business logic for the CNAB file processing system.

## Overview

The system processes CNAB files containing transactional data. Each file must be processed completely or rejected entirely — no partial processing is allowed.

## CNAB File Format

The system processes CNAB files with the following fixed-width structure (80 characters per line):

| Field | Start | End | Length | Description |
|-------|-------|-----|--------|-------------|
| Type | 1 | 1 | 1 | Transaction type (1-9) |
| Date | 2 | 9 | 8 | Occurrence date (YYYYMMDD) |
| Amount | 10 | 19 | 10 | Transaction amount (cents, divide by 100.00) |
| CPF | 20 | 30 | 11 | Recipient CPF |
| Card | 31 | 42 | 12 | Card used in transaction |
| Time | 43 | 48 | 6 | Occurrence time (HHMMSS) |
| Store Owner | 49 | 62 | 14 | Store representative name |
| Store Name | 63 | 81 | 19 | Store name |

### Transaction Types

| Type | Description | Nature | Sign |
|------|-------------|--------|------|
| 1 | Debit | Inflow | + |
| 2 | Bank Slip (Boleto) | Outflow | - |
| 3 | Financing | Outflow | - |
| 4 | Credit | Inflow | + |
| 5 | Loan Receipt | Inflow | + |
| 6 | Sales | Inflow | + |
| 7 | TED Receipt | Inflow | + |
| 8 | DOC Receipt | Inflow | + |
| 9 | Rent | Outflow | - |

**Note**: Inflow transactions (+) increase store balance, while Outflow transactions (-) decrease it.

## CNAB File Processing

### File Upload

- Files are uploaded via the web frontend
- Users can upload CNAB files through a simple interface
- The UI provides immediate non-blocking feedback

### File Validation

The backend validates:

1. **File Structure**
   - Valid CNAB format
   - Proper encoding

2. **Layout Consistency**
   - All lines follow the same structure
   - Consistent field positions

3. **Line Length**
   - Each line must have exactly 80 characters (standard CNAB format)

4. **Record Types**
   - Valid transaction types
   - Proper record type indicators

### Transactional Processing

**Critical Rule**: Processing is **transactional per file**.

- If **any** line fails validation, the **entire file** is rejected
- If **any** line fails persistence, the **entire file** is rejected
- No partial persistence is allowed
- All-or-nothing guarantee

This ensures data integrity and prevents inconsistent states.

## File States

Files progress through the following states:

1. **Uploaded** - File has been received and stored
2. **Processing** - File is being validated and processed
3. **Processed (Success)** - All transactions were successfully persisted
4. **Rejected (Error)** - File was rejected due to validation or processing errors

### State Transitions

```
Uploaded → Processing → Processed (Success)
               ↓
            Rejected (Error)
```

Once a file reaches **Processed** or **Rejected**, it cannot transition to another state.

## Transactions

### Transaction Model

- Each transaction is linked to a **Store**
- Transactions contain:
   - Transaction type (credit/debit)
   - Amount
   - Date and time
   - Store information
   - Additional metadata

### Balance Calculation

- Balance is calculated **per Store**
- Balance aggregates all transactions for a specific store
- Balance considers transaction types (credits increase, debits decrease)

### Query Capabilities

The system supports transaction queries with:

1. **Store Filters**
   - Filter by specific store
   - Filter by store name (partial match)

2. **Date Range Filters**
   - Filter by transaction date
   - Support for date ranges (from/to)

3. **Aggregations**
   - Total balance per store
   - Transaction count
   - Summary statistics

## Validation Rules

### File-Level Validation

- File must be a valid text file
- File must contain at least one transaction line
- File encoding must be correct

### Line-Level Validation

Each line (80 characters) must contain:

- Valid transaction type (position 1)
- Valid date (positions 2-9)
- Valid amount (positions 10-19)
- Valid CPF (positions 20-30)
- Valid card number (positions 31-42)
- Valid time (positions 43-48)
- Valid store owner name (positions 49-62)
- Valid store name (positions 63-80)

### Business Rule Validation

- Transaction types must be recognized
- Dates must be valid
- Amounts must be positive numbers
- CPF must have a valid format
- Store information must be present

## Rejection Rules

### When a File Is Rejected

A file is rejected when:

1. **Validation Errors**
   - Invalid file format
   - Inconsistent line structure
   - Invalid fields
   - Business rule violations

2. **Processing Errors**
   - Database errors
   - Parsing errors
   - System failures

### Rejection Behavior

When a file is rejected:

- Status is set to **Rejected**
- Error message is stored
- No data is persisted
- User receives error notification
- System remains in a consistent state

## Store Balance Calculation

### Calculation Rules

- Initial balance: 0.00
- Credits: increase balance
- Debits: decrease balance
- Calculation is performed per store
- Balance is calculated in real-time from transactions

### Calculation Example

```
Store: MERCADO DA AVENIDA
Transactions:
   - Debit: R$ 50.00
   - Credit: R$ 100.00
   - Debit: R$ 25.00
   - Credit: R$ 200.00

Final Balance: R$ 225.00
```

## Available Filters

### Store Filter

- Exact search by store name
- Partial search (LIKE)
- Case-insensitive
- Supports multiple stores (if implemented)

### Date Filter

- Start date (from)
- End date (to)
- Date range
- Valid range validation

### Combining Filters

- Filters can be combined
- AND operation between filters
- Results ordered by date (most recent first)

## Business Logic Summary

1. **Atomicity**: Each file is processed as a single atomic operation
2. **Consistency**: All transactions in a file are validated together
3. **Isolation**: File processing does not interfere with each other
4. **Durability**: Only successfully processed files are persisted

These rules ensure data integrity and provide a reliable system for processing financial transaction files.

---

**Last Updated**: January 14, 2026
