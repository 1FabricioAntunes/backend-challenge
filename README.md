# Programming Challenge - Back-end Position

Please read this document carefully from beginning to end. The purpose of this test is to assess your technical programming skills. The challenge consists of parsing this text file (https://github.com/ByCodersTec/desafio-ruby-on-rails/blob/master/CNAB.txt) and saving its information (financial transactions) into a database of your choice. This challenge should be completed by you at home. Take as much time as you need, but usually you shouldn't need more than a few hours.

## Challenge Submission Instructions

1.  First, fork this project to your Github account (create one if you don't have it).
2.  Implement the project as described below in your local clone.
3.  Send the project or the fork/link to your ByCoders contact with a copy to **rh@bycoders.com.br**.

## Project Description

You received a CNAB file with financial transaction data from several stores. We need to create a way for this data to be imported into a database. Your task is to create a web interface that accepts uploads of the CNAB file (https://github.com/ByCodersTec/desafio-ruby-on-rails/blob/master/CNAB.txt), normalizes the data, stores it in a relational database, and displays this information on the screen.

## Web Application Requirements

Your web application **MUST**:

1.  Have a screen (via a form) to upload the file\
    *Extra points if you don't use a popular CSS framework.*

2.  Parse the received file, normalize the data, and correctly save it
    in a relational database\
    *(Pay attention to the CNAB documentation below.)*

3.  Display a **list of imported operations by store**, including a
    **total account balance**

4.  Be written in your preferred programming language

5.  Be simple to configure and run in a Unix-based system (Linux or
    macOS)\
    *(Use only free/open-source languages and libraries.)*

6.  Use **Git** with atomic and well-described commits

7.  Use **PostgreSQL, MySQL, or SQL Server**

8.  Have **automated tests**

9.  Use **Docker Compose**\
    *Extra points if you use it.*

10. Include a **README** describing the project and its setup

11. Include instructions describing **how to consume the API endpoint**

## The application does NOT need to:

1.  Handle authentication or authorization\
    *Extra points if implemented; even more if OAuth.*

2.  Document the API\
    *Optional --- but earns extra points.*

## CNAB Documentation

| Field        | Start | End | Size | Description                                 |
|--------------|:-----:|:---:|:----:|---------------------------------------------|
| Type         | 1     | 1   | 1    | Transaction type                             |
| Date         | 2     | 9   | 8    | Date of occurrence                           |
| Value        | 10    | 19  | 10   | Transaction amount (divide by 100.00)        |
| CPF          | 20    | 30  | 11   | Beneficiary's CPF                            |
| Card         | 31    | 42  | 12   | Card used in the transaction                 |
| Time         | 43    | 48  | 6    | Time of occurrence (UTC-3)                   |
| Store Owner  | 49    | 62  | 14   | Store representative name                    |
| Store Name   | 63    | 81  | 19   | Store name                                   |

## Transaction Types

| Type | Description    | Nature  | Sign |
|------|----------------|---------|------|
| 1    | Debit          | Income  | +    |
| 2    | Boleto         | Expense | -    |
| 3    | Financing      | Expense | -    |
| 4    | Credit         | Income  | +    |
| 5    | Loan Receipt   | Income  | +    |
| 6    | Sales          | Income  | +    |
| 7    | TED Receipt    | Income  | +    |
| 8    | DOC Receipt    | Income  | +    |
| 9    | Rent           | Expense | -    |


## Evaluation Criteria

Your project will be evaluated based on:

1.  Whether your application meets the basic requirements
2.  Documentation on environment setup and application execution
3.  Whether you followed the challenge submission instructions
4.  Quality and coverage of automated tests

We will also assess:

-   Your familiarity with standard libraries
-   Your experience with object-oriented programming
-   The structure and maintainability of your project

## Good luck!
