# 1.3.0

Implemented AT.ARG for replacing an argument in method calls and ArgIndex for targeting the arg
Implemented AT.FINALLY to run code regardless of if it throws or not
Implemented AT.LOOP_BEFORE/TOP/BOTTOM/AFTER
Implemented BRANCH_TRUE/FALSE
Implemented LOCAL_WRITE/READ
Implemented ARG_WRITE/READ
Implemented FIELD_WRITE/READ

Internally implemented more helpers, tests and a test console project
Added lots of documentation

# 1.2.1

Updated xml comments
Fixed some PatchAttribute logic
Updated readme with more examples

# 1.2.0

WARNING: Changed AT.RETURN to AT.POSTFIX

AT.RETURN now injects the code before every return in a method

Added warning when a mod is referencing an outdated version
Internally extracted some classes into their own files
Fixed nullref exceptions when using REDIRECT/INVOKE/AFTER with a null target
Added signature validation to REDIRECT

# 1.1.0

Update readme to add a note for publicizers

# 1.0.0

Uploaded
