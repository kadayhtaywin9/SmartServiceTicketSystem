DROP DATABASE IF EXISTS smart_ticket_db;

CREATE DATABASE smart_ticket_db
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE smart_ticket_db;

CREATE TABLE roles (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(50) NOT NULL UNIQUE,
    description VARCHAR(255) NULL
) ENGINE=InnoDB;

CREATE TABLE permissions (
    id INT AUTO_INCREMENT PRIMARY KEY,
    slug VARCHAR(100) NOT NULL UNIQUE,
    module VARCHAR(50) NOT NULL
) ENGINE=InnoDB;

CREATE TABLE role_permissions (
    role_id INT NOT NULL,
    permission_id INT NOT NULL,
    PRIMARY KEY (role_id, permission_id),
    CONSTRAINT fk_rp_role FOREIGN KEY (role_id) REFERENCES roles(id) ON DELETE CASCADE,
    CONSTRAINT fk_rp_perm FOREIGN KEY (permission_id) REFERENCES permissions(id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE departments (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(100) NOT NULL UNIQUE,
    keywords VARCHAR(500) NULL,
    lead_user_id INT NULL
) ENGINE=InnoDB;

CREATE TABLE users (
    id INT AUTO_INCREMENT PRIMARY KEY,
    role_id INT NOT NULL,
    department_id INT NULL,
    full_name VARCHAR(120) NOT NULL,
    email VARCHAR(150) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    skills VARCHAR(255) NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_user_role FOREIGN KEY (role_id) REFERENCES roles(id),
    CONSTRAINT fk_user_dept FOREIGN KEY (department_id) REFERENCES departments(id) ON DELETE SET NULL
) ENGINE=InnoDB;

ALTER TABLE departments
    ADD CONSTRAINT fk_dept_lead FOREIGN KEY (lead_user_id) REFERENCES users(id) ON DELETE SET NULL;

CREATE TABLE user_assets (
    id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    asset_tag VARCHAR(50) NOT NULL UNIQUE,
    device_model VARCHAR(120) NOT NULL,
    os_version VARCHAR(80) NOT NULL,
    serial_number VARCHAR(100) NOT NULL,
    assigned_date DATE NOT NULL,
    CONSTRAINT fk_asset_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE sla_policies (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    priority ENUM('Low', 'Medium', 'High', 'Critical') NOT NULL UNIQUE,
    first_response_mins INT NOT NULL,
    resolution_mins INT NOT NULL,
    business_hours_only BOOLEAN NOT NULL DEFAULT TRUE
) ENGINE=InnoDB;

CREATE TABLE tickets (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    ticket_code VARCHAR(30) NOT NULL UNIQUE,
    parent_ticket_id BIGINT NULL,
    requester_id INT NOT NULL,
    assignee_id INT NULL,
    department_id INT NOT NULL,
    sla_policy_id INT NOT NULL,
    source_channel ENUM('WebPortal', 'Email', 'Telegram', 'MonitoringWebhook') NOT NULL DEFAULT 'WebPortal',
    subject VARCHAR(255) NOT NULL,
    description TEXT NOT NULL,
    status ENUM('Open', 'In_Progress', 'Pending_Customer', 'Resolved', 'Closed') NOT NULL DEFAULT 'Open',
    priority ENUM('Low', 'Medium', 'High', 'Critical') NOT NULL DEFAULT 'Medium',
    sentiment_label ENUM('Calm', 'Frustrated', 'Angry') NOT NULL DEFAULT 'Calm',
    sentiment_score DECIMAL(4,2) NOT NULL DEFAULT 0.10,
    ai_reasoning TEXT NULL,
    escalation_level INT NOT NULL DEFAULT 0,
    first_response_due_at DATETIME NOT NULL,
    resolution_due_at DATETIME NOT NULL,
    first_responded_at DATETIME NULL,
    resolved_at DATETIME NULL,
    is_sla_breached BOOLEAN NOT NULL DEFAULT FALSE,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT fk_ticket_parent FOREIGN KEY (parent_ticket_id) REFERENCES tickets(id) ON DELETE SET NULL,
    CONSTRAINT fk_ticket_requester FOREIGN KEY (requester_id) REFERENCES users(id),
    CONSTRAINT fk_ticket_assignee FOREIGN KEY (assignee_id) REFERENCES users(id) ON DELETE SET NULL,
    CONSTRAINT fk_ticket_dept FOREIGN KEY (department_id) REFERENCES departments(id),
    CONSTRAINT fk_ticket_sla FOREIGN KEY (sla_policy_id) REFERENCES sla_policies(id),
    INDEX idx_ticket_status_prio (status, priority),
    INDEX idx_ticket_dept (department_id),
    INDEX idx_ticket_sla (resolution_due_at, is_sla_breached)
) ENGINE=InnoDB;

CREATE TABLE ticket_messages (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    ticket_id BIGINT NOT NULL,
    sender_id INT NOT NULL,
    body TEXT NOT NULL,
    is_internal BOOLEAN NOT NULL DEFAULT FALSE,
    contains_masked_pii BOOLEAN NOT NULL DEFAULT FALSE,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_msg_ticket FOREIGN KEY (ticket_id) REFERENCES tickets(id) ON DELETE CASCADE,
    CONSTRAINT fk_msg_sender FOREIGN KEY (sender_id) REFERENCES users(id),
    INDEX idx_msg_ticket (ticket_id, created_at)
) ENGINE=InnoDB;

CREATE TABLE ticket_attachments (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    ticket_id BIGINT NOT NULL,
    uploaded_by INT NOT NULL,
    file_name VARCHAR(255) NOT NULL,
    stored_name VARCHAR(255) NOT NULL,
    content_type VARCHAR(120) NOT NULL,
    file_size BIGINT NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_att_ticket FOREIGN KEY (ticket_id) REFERENCES tickets(id) ON DELETE CASCADE,
    CONSTRAINT fk_att_user FOREIGN KEY (uploaded_by) REFERENCES users(id),
    INDEX idx_att_ticket (ticket_id)
) ENGINE=InnoDB;

CREATE TABLE kb_articles (
    id INT AUTO_INCREMENT PRIMARY KEY,
    category VARCHAR(80) NOT NULL,
    title VARCHAR(255) NOT NULL,
    tags VARCHAR(255) NOT NULL,
    content TEXT NOT NULL,
    helpful_count INT NOT NULL DEFAULT 0,
    is_published BOOLEAN NOT NULL DEFAULT TRUE,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

CREATE TABLE ticket_audit_logs (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    ticket_id BIGINT NULL,
    actor_id INT NULL,
    actor_name VARCHAR(120) NOT NULL,
    action VARCHAR(80) NOT NULL,
    old_value JSON NULL,
    new_value JSON NULL,
    ip_address VARCHAR(45) NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_audit_ticket FOREIGN KEY (ticket_id) REFERENCES tickets(id) ON DELETE SET NULL,
    INDEX idx_audit_ticket (ticket_id, created_at)
) ENGINE=InnoDB;

DELIMITER $$
CREATE TRIGGER trg_prevent_audit_update
BEFORE UPDATE ON ticket_audit_logs
FOR EACH ROW
BEGIN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'COMPLIANCE VIOLATION: Audit logs are immutable.';
END$$

CREATE TRIGGER trg_prevent_audit_delete
BEFORE DELETE ON ticket_audit_logs
FOR EACH ROW
BEGIN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'COMPLIANCE VIOLATION: Audit logs are immutable.';
END$$
DELIMITER ;

CREATE TABLE csat_surveys (
    id INT AUTO_INCREMENT PRIMARY KEY,
    ticket_id BIGINT NOT NULL UNIQUE,
    rating TINYINT NOT NULL CHECK (rating BETWEEN 1 AND 5),
    feedback TEXT NULL,
    submitted_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_csat_ticket FOREIGN KEY (ticket_id) REFERENCES tickets(id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- ============================================================================
-- COMPANY SEED DATA (Software Dev, IT & Network, HR, Finance, Cybersecurity)
-- ============================================================================
INSERT INTO roles (id, name, description) VALUES
(1, 'SuperAdmin', 'Full system governance, user onboarding, security, audit logs, and SLA configuration'),
(2, 'DepartmentLead', 'Manages department queues, escalations, reports, and parent-child incidents'),
(3, 'SupportAgent', 'Handles assigned tickets, attachments, internal notes, and knowledge base guides'),
(4, 'EndUser', 'Company employee or client submitting service requests and CSAT ratings');

INSERT INTO permissions (id, slug, module) VALUES
(1, 'ticket.create', 'Tickets'),
(2, 'ticket.view_all', 'Tickets'),
(3, 'ticket.assign', 'Tickets'),
(4, 'ticket.parent_link', 'Workflow'),
(5, 'note.internal', 'Collaboration'),
(6, 'sla.override', 'SLA'),
(7, 'pii.unmask', 'Security'),
(8, 'audit.view', 'Compliance'),
(9, 'admin.manage', 'Admin');

INSERT INTO role_permissions (role_id, permission_id) SELECT 1, id FROM permissions;
INSERT INTO role_permissions (role_id, permission_id) VALUES (2,1),(2,2),(2,3),(2,4),(2,5),(2,6),(2,8),(2,9);
INSERT INTO role_permissions (role_id, permission_id) VALUES (3,1),(3,2),(3,4),(3,5);
INSERT INTO role_permissions (role_id, permission_id) VALUES (4,1);

INSERT INTO departments (id, name, keywords) VALUES
(1, 'Software Development & QA', 'bug,api,deploy,git,ci/cd,production,crash,database,release,frontend,backend,sprint,502,500,code,feature'),
(2, 'IT & Network Infrastructure', 'vpn,wifi,network,server,router,firewall,laptop,printer,email,outage,down,ssl,domain,hardware,slow'),
(3, 'Human Resources (HR)', 'leave,payroll,salary,onboarding,offboarding,contract,attendance,benefits,id card,hr,recruit,letter'),
(4, 'Finance & Administration', 'invoice,reimbursement,expense,budget,purchase,procurement,office,facility,travel,billing,receipt'),
(5, 'Cybersecurity & Compliance', 'phishing,breach,password,mfa,sso,access,permission,security,audit,malware,suspicious,locked');

INSERT INTO users (id, role_id, department_id, full_name, email, password_hash, skills) VALUES
(1, 1, 2, 'Kaday Admin', 'admin@company.local', 'DEMO:Admin@123', 'all'),
(2, 2, 2, 'Thura (IT & Net Lead)', 'lead@company.local', 'DEMO:Lead@123', 'network,server,vpn,escalation'),
(3, 3, 1, 'Su Myat (DevOps & QA)', 'dev@company.local', 'DEMO:Dev@123', 'bug,api,deploy,database,production,frontend,backend'),
(4, 3, 2, 'Aung Ko (NetOps Eng)', 'it@company.local', 'DEMO:It@123', 'vpn,wifi,network,firewall,laptop,printer'),
(5, 3, 3, 'Hnin Wai (HR Specialist)', 'hr@company.local', 'DEMO:Hr@123', 'leave,payroll,onboarding,contract,benefits'),
(6, 4, NULL, 'Min Khant (Employee)', 'employee@company.local', 'DEMO:User@123', NULL);

UPDATE departments SET lead_user_id = 2 WHERE id IN (2, 5);
UPDATE departments SET lead_user_id = 3 WHERE id = 1;
UPDATE departments SET lead_user_id = 5 WHERE id IN (3, 4);

INSERT INTO user_assets (user_id, asset_tag, device_model, os_version, serial_number, assigned_date) VALUES
(6, 'AST-2026-104', 'Acer Aspire 5 / Ryzen 7', 'Windows 11 Pro 24H2', 'NX-A515-45-9921', '2026-02-10');

INSERT INTO sla_policies (id, name, priority, first_response_mins, resolution_mins, business_hours_only) VALUES
(1, 'Standard Low SLA', 'Low', 240, 1440, TRUE),
(2, 'Standard Medium SLA', 'Medium', 120, 480, TRUE),
(3, 'Priority High SLA', 'High', 30, 240, TRUE),
(4, 'Critical 24/7 Outage SLA', 'Critical', 15, 120, FALSE);

INSERT INTO kb_articles (category, title, tags, content, helpful_count) VALUES
('Software Dev & QA', 'Production Bug & Hotfix Escalation Protocol', 'bug,api,502,production,crash,deploy',
 '1. Capture the exact timestamp, Request ID, and screenshot of the error.\n2. Attach browser console or server logs to your ticket.\n3. Critical P1 bugs trigger immediate on-call DevOps response within 15 minutes.', 86),
('IT & Network', 'Fix Office VPN Error 809 & Wi-Fi Certificate', 'vpn,wifi,timeout,network,error 809,connect',
 '1. Open Cisco AnyConnect / WireGuard client.\n2. Clear cached credentials and switch gateway to Asia-SG-02.\n3. For Office Wi-Fi, re-import the 2026 corporate root certificate.', 142),
('Human Resources', 'Annual Leave, Medical Leave & Payroll Inquiry Guide', 'leave,payroll,salary,onboarding,attendance,hr',
 '1. Submit annual leave requests at least 3 business days ahead.\n2. Attach medical certificates for sick leave over 2 days.\n3. Payroll discrepancy tickets are resolved before the 28th of each month.', 115),
('Cybersecurity', 'Resetting Corporate SSO & MFA Authenticator', 'login,sso,password,mfa,locked,security',
 '1. Visit https://sso.company.local/recovery using your backup code.\n2. If you lost your phone, submit a High priority ticket to Cybersecurity & Compliance for manual MFA reset.', 98),
('Finance & Admin', 'Expense Reimbursement & Equipment Purchase', 'invoice,reimbursement,expense,purchase,billing,receipt',
 '1. Attach scanned tax receipts or vendor quotations to the ticket.\n2. Manager approval is required for purchases exceeding 300,000 MMK.', 64);

INSERT INTO tickets (id, ticket_code, parent_ticket_id, requester_id, assignee_id, department_id, sla_policy_id, source_channel, subject, description, status, priority, sentiment_label, sentiment_score, ai_reasoning, first_response_due_at, resolution_due_at)
VALUES
(1, 'TKT-2026-000201', NULL, 2, 4, 2, 4, 'MonitoringWebhook',
 'Core Singapore VPN Gateway High Packet Loss',
 'Prometheus alert triggered: sg-vpn-gw01 packet loss exceeded 18%. Multiple remote engineers affected.',
 'In_Progress', 'Critical', 'Calm', 0.20,
 'Step 1 (Routing): Matched keywords [vpn, server] -> IT & Network Infrastructure.\nStep 2 (Severity): Monitoring outage alert -> Critical 24/7 SLA.\nStep 3 (Assignment): Assigned to Aung Ko (NetOps Eng).',
 DATE_ADD(NOW(), INTERVAL 12 MINUTE), DATE_ADD(NOW(), INTERVAL 95 MINUTE)),

(2, 'TKT-2026-000202', 1, 6, 4, 2, 3, 'WebPortal',
 'VPN keeps disconnecting during client deployment',
 'My VPN drops every 2 minutes and I cannot push to our staging server. Please call [MASKED-PHONE] urgently.',
 'In_Progress', 'High', 'Angry', 0.86,
 'Step 1 (Sentiment): Detected urgency markers -> Sentiment Angry (86%), Priority elevated to High.\nStep 2 (PII Shield): Masked phone number automatically.\nStep 3 (Incident): Linked under Master Incident TKT-2026-000201.',
 DATE_ADD(NOW(), INTERVAL 22 MINUTE), DATE_ADD(NOW(), INTERVAL 180 MINUTE)),

(3, 'TKT-2026-000203', NULL, 6, 3, 1, 3, 'WebPortal',
 'Production Checkout API returning HTTP 502 after v2.4 release',
 'After today''s backend deployment, POST /api/orders intermittently throws 502 Bad Gateway under load.',
 'Open', 'High', 'Frustrated', 0.62,
 'Step 1 (Routing): Matched keywords [api, production, 502, release] -> Software Development & QA.\nStep 2 (Assignment): Matched skill tags [api, production] -> Su Myat (DevOps & QA).',
 DATE_ADD(NOW(), INTERVAL 28 MINUTE), DATE_ADD(NOW(), INTERVAL 220 MINUTE)),

(4, 'TKT-2026-000204', NULL, 6, 5, 3, 2, 'WebPortal',
 'New Intern Onboarding Laptop & Payroll Setup',
 'Requesting HR onboarding checklist, employee ID card, and bank payroll registration for our new junior developer starting Monday.',
 'Open', 'Medium', 'Calm', 0.15,
 'Step 1 (Routing): Matched keywords [onboarding, payroll, id card, hr] -> Human Resources (HR).\nStep 2 (Assignment): Matched skill tags [onboarding, payroll] -> Hnin Wai (HR Specialist).',
 DATE_ADD(NOW(), INTERVAL 110 MINUTE), DATE_ADD(NOW(), INTERVAL 450 MINUTE));

INSERT INTO ticket_messages (ticket_id, sender_id, body, is_internal, contains_masked_pii) VALUES
(1, 2, 'Prometheus alert triggered: sg-vpn-gw01 packet loss exceeded 18%. Multiple remote engineers affected.', FALSE, FALSE),
(1, 4, 'Checking BGP routing table on SG-GW-01. Failover to Asia-SG-02 in progress.', TRUE, FALSE),
(2, 6, 'My VPN drops every 2 minutes and I cannot push to our staging server. Please call [MASKED-PHONE] urgently.', FALSE, TRUE),
(2, 4, 'Linked to Master Incident TKT-2026-000201.', TRUE, FALSE),
(2, 4, 'Hi Min Khant, we are rerouting VPN traffic to Asia-SG-02 now. Connection will stabilize within 10 minutes.', FALSE, FALSE),
(3, 6, 'After today''s backend deployment, POST /api/orders intermittently throws 502 Bad Gateway under load.', FALSE, FALSE),
(4, 6, 'Requesting HR onboarding checklist, employee ID card, and bank payroll registration for our new junior developer starting Monday.', FALSE, FALSE);

INSERT INTO ticket_audit_logs (ticket_id, actor_id, actor_name, action, old_value, new_value, ip_address) VALUES
(1, 2, 'Thura (IT & Net Lead)', 'TICKET_CREATED', NULL, '{"status":"In_Progress","priority":"Critical","dept":"IT & Network"}', '10.24.0.10'),
(2, 6, 'Min Khant (Employee)', 'TICKET_CREATED', NULL, '{"status":"Open","priority":"High","sentiment":"Angry"}', '10.24.18.42'),
(2, 4, 'Aung Ko (NetOps Eng)', 'PARENT_TICKET_LINKED', '{"parent_ticket_id":null}', '{"parent_code":"TKT-2026-000201"}', '10.24.10.15'),
(3, 6, 'Min Khant (Employee)', 'TICKET_CREATED', NULL, '{"status":"Open","priority":"High","dept":"Software Dev & QA"}', '10.24.18.42'),
(4, 6, 'Min Khant (Employee)', 'TICKET_CREATED', NULL, '{"status":"Open","priority":"Medium","dept":"Human Resources (HR)"}', '10.24.18.42');
