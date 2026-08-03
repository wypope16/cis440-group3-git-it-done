-- US-07: Stores the company-wide number of days required
-- between accepted employee check-ins.
CREATE TABLE IF NOT EXISTS checkin_settings (
    setting_id TINYINT NOT NULL,
    frequency_days INT NOT NULL DEFAULT 7,
    updated_at TIMESTAMP NOT NULL
        DEFAULT CURRENT_TIMESTAMP
        ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (setting_id)
) ENGINE=InnoDB
DEFAULT CHARACTER SET utf8mb4
COLLATE utf8mb4_unicode_ci;

-- The project uses one company-wide frequency setting.
-- INSERT IGNORE prevents a duplicate row if this script is run again.
INSERT IGNORE INTO checkin_settings (
    setting_id,
    frequency_days
)
VALUES (
    1,
    7
);

-- Stores only an anonymous control token and the time of the
-- most recent accepted submission for that token.
--
-- This table does not store the employee's mood, explanation,
-- recommendation, name, email address, or employee ID.
CREATE TABLE IF NOT EXISTS checkin_frequency_log (
    anonymous_token VARCHAR(128) NOT NULL,
    last_submitted_at DATETIME NOT NULL,
    PRIMARY KEY (anonymous_token)
) ENGINE=InnoDB
DEFAULT CHARACTER SET utf8mb4
COLLATE utf8mb4_unicode_ci;