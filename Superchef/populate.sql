-- Generate numbers 0–11 for 30-minute slots between 7:00–18:00
WITH Hours AS (
    SELECT 7 AS H
    UNION ALL
    SELECT H + 1 FROM Hours WHERE H + 1 <= 18
),
Minutes AS (
    SELECT 0 AS M
    UNION ALL
    SELECT 30
),
Days AS (
    SELECT 0 AS DayOfWeek
    UNION ALL
    SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3
    UNION ALL SELECT 4 UNION ALL SELECT 5 UNION ALL SELECT 6
)
INSERT INTO SlotTemplates (StartTime, DayOfWeek)
SELECT 
    CAST(CONCAT(H.H, ':', FORMAT(M.M, '00'), ':00') AS TIME) AS StartTime,
    D.DayOfWeek
FROM Days D
CROSS JOIN Hours H
CROSS JOIN Minutes M
ORDER BY D.DayOfWeek, H.H, M.M
OPTION (MAXRECURSION 0);

-- Create account types
DBCC CHECKIDENT ('AccountTypes', RESEED, 0);
SET IDENTITY_INSERT [dbo].[AccountTypes] ON
INSERT INTO [dbo].[AccountTypes] ([Id], [Name]) VALUES (1, N'Customer')
INSERT INTO [dbo].[AccountTypes] ([Id], [Name]) VALUES (2, N'Vendor')
INSERT INTO [dbo].[AccountTypes] ([Id], [Name]) VALUES (3, N'Admin')
SET IDENTITY_INSERT [dbo].[AccountTypes] OFF

-- Create categories
DBCC CHECKIDENT ('Categories', RESEED, 0);
INSERT INTO Categories (Name, Image)
VALUES
('Other', 'other.png'),
('Rice Meals', 'image2.png'),
('Noodles', 'image3.png'),
('Western Food', 'image4.png'),
('Fast Food', 'image5.png'),
('Vegetarian', 'image6.png'),
('Healthy Meals', 'image7.png'),
('Bento & Lunch Boxes', 'image8.png'),
('Malay Cuisine', 'image9.png'),
('Indian Cuisine', 'image10.png'),
('Chinese Cuisine', 'image11.png'),
('Korean Food', 'image12.png'),
('Japanese Food', 'image13.png'),
('Snacks', 'image14.png'),
('Desserts', 'image15.png'),
('Bakery', 'image16.png'),
('Beverages', 'image17.png');

-- Create venue
SET IDENTITY_INSERT [dbo].[Venues] ON
INSERT INTO [dbo].[Venues] ([Id], [Name]) VALUES (1, N'Red Brick')
INSERT INTO [dbo].[Venues] ([Id], [Name]) VALUES (2, N'Yum Yum')
SET IDENTITY_INSERT [dbo].[Venues] OFF
