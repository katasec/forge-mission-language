grammar MclGrammar;

// Parser rules

program
    : (letBinding | declaration | outputDecl)* EOF
    ;

letBinding
    : LET LOWER_ID EQUALS value
    ;

declaration
    : mission
    ;

outputDecl
    : OUTPUT LPAREN UPPER_ID (COMMA STRING)? RPAREN
    ;

mission
    : MISSION UPPER_ID params? loopClause? EQUALS LBRACE pipeline RBRACE
    ;

loopClause
    : LOOP LPAREN INT RPAREN
    ;

params
    : LPAREN LOWER_ID (COMMA LOWER_ID)* RPAREN
    ;

pipeline
    : pipelineElement (ARROW pipelineElement)*
    ;

pipelineElement
    : step
    | parallelBlock
    ;

step
    : UPPER_ID contextClause? usingClause? whenClause?
    ;

contextClause
    : LPAREN binding (COMMA binding)* RPAREN
    ;

usingClause
    : USING LOWER_ID
    ;

whenClause
    : WHEN LPAREN whenExpr RPAREN
    ;

whenExpr
    : anyKey COLON STRING    # StringEquals
    | ELSE                   # ElseExpr
    ;

parallelBlock
    : PARALLEL LBRACE step+ RBRACE
    ;

binding
    : anyKey COLON value
    ;

// anyKey allows keyword tokens to be used as binding/when keys (e.g. when(output: "x"))
anyKey
    : LOWER_ID
    | LET | MISSION | LOOP | USING | WHEN | PARALLEL | ENV | OUTPUT
    ;

value
    : STRING
    | LOWER_ID
    | INT
    | envCall
    ;

envCall
    : ENV LPAREN STRING (COMMA STRING)? RPAREN
    ;

// Lexer rules — keywords listed before LOWER_ID so they take priority

LET      : 'let'      ;
MISSION  : 'mission'  ;
LOOP     : 'loop'     ;
USING    : 'using'    ;
WHEN     : 'when'     ;
ELSE     : 'else'     ;
PARALLEL : 'parallel' ;
ENV      : 'env'      ;
OUTPUT   : 'output'   ;

INT      : [0-9]+     ;
ARROW    : '->'       ;
EQUALS   : '='        ;
COLON    : ':'        ;
LPAREN   : '('        ;
RPAREN   : ')'        ;
LBRACE   : '{'        ;
RBRACE   : '}'        ;
COMMA    : ','        ;

UPPER_ID
    : [A-Z][a-zA-Z0-9]*
    ;

LOWER_ID
    : [a-z][a-zA-Z0-9]*
    ;

STRING
    : '"' (~["\r\n])* '"'
    ;

WS
    : [ \t\r\n]+ -> skip
    ;

LINE_COMMENT
    : '//' ~[\r\n]* -> skip
    ;
