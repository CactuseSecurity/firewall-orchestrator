-- issue #5205: the violation type cannot always be derived from the criterion, e.g. a criterion that
-- could not be evaluated for the address family of an object is recorded as not assessable
ALTER TABLE compliance.violation
ADD COLUMN IF NOT EXISTS violation_type TEXT;
