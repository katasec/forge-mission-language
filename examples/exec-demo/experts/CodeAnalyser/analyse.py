#!/usr/bin/env python3
"""
CodeAnalyser — measures structural properties of a code snippet.
Reads JSON from stdin, writes JSON metrics to stdout.
"""
import sys
import json
import ast
import re

def analyse(code: str) -> dict:
    lines       = code.splitlines()
    blank_lines = sum(1 for l in lines if not l.strip())
    code_lines  = len(lines) - blank_lines

    # Count functions and classes via AST (Python code only; falls back gracefully)
    function_count = 0
    class_count    = 0
    try:
        tree           = ast.parse(code)
        function_count = sum(1 for n in ast.walk(tree) if isinstance(n, (ast.FunctionDef, ast.AsyncFunctionDef)))
        class_count    = sum(1 for n in ast.walk(tree) if isinstance(n, ast.ClassDef))
    except SyntaxError:
        # Non-Python code — use regex heuristics
        function_count = len(re.findall(r'\bdef\b|\bfunc\b|\bfunction\b', code))
        class_count    = len(re.findall(r'\bclass\b|\bstruct\b', code))

    # Cyclomatic complexity proxy: count branch keywords
    branch_keywords = re.findall(r'\b(if|elif|else|for|while|try|except|case)\b', code)

    return {
        "metrics": {
            "total_lines":    len(lines),
            "code_lines":     code_lines,
            "blank_lines":    blank_lines,
            "function_count": function_count,
            "class_count":    class_count,
            "branch_count":   len(branch_keywords),
            "complexity":     "low" if len(branch_keywords) < 5 else "medium" if len(branch_keywords) < 15 else "high",
        }
    }

if __name__ == "__main__":
    data = json.load(sys.stdin)
    path = data.get("repo_path", "")
    code = open(path).read() if path else ""
    print(json.dumps(analyse(code)))
